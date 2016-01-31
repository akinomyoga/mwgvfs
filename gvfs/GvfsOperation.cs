//#define LOG_QUERY

using Gen=System.Collections.Generic;
using Compiler=System.Runtime.CompilerServices;
using Diag=System.Diagnostics;
using Ref=System.Reflection;

using jsch=Tamir.SharpSsh.jsch;
using java=Tamir.SharpSsh.java;
using Dokan;

using mwg.Sshfs;

namespace mwg.mwgvfs.gvfs{

  [System.Flags,System.Serializable]
  public enum SetFileInfoFlags{
    SetPermission    =0x0001,
    SetFileSize      =0x0002,
    SetFileTime      =0x0070,
    SetFileCTime     =0x0010,
    SetFileMTime     =0x0020,
    SetFileATime     =0x0040,
  }

  static class FsBasicUtil{
    public const int ErrorRequestReconnection =0x10001;
    public const int ErrorUnknownAlternateData=0x10002;

    public static int ReadDataFromArray(byte[] data,long offset,byte[] buff,int buffOffset,ref int length){
      if(data==null)
        goto file_not_found;
      if(offset<0||buff.Length<buffOffset)
        goto invalid_argument;

      if(length>buff.Length-buffOffset)
        length=buff.Length-buffOffset;
      if(length>data.Length-offset)
        length=data.Length-(int)offset;

      if(length>0)
        System.Array.Copy(data,offset,buff,buffOffset,length);
      else
        length=0;
      return 0;
    file_not_found:
      length=0;
      return -WinErrorCode.ERROR_FILE_NOT_FOUND;
    invalid_argument:
      length=0;
      return -1;
    }
  }
  class FsBasicReturnException:System.Exception{
    public readonly int returnCode;
    public FsBasicReturnException(int returnCode):base(){
      this.returnCode=returnCode;
    }
  }

  /// <summary>
  /// ファイルシステムの基本機能を実装します。
  /// </summary>
  public interface IFsBasic:System.IDisposable{
    bool IsReadOnly{get;}
    void Disconnect();
    bool Reconnect();
    int ReconnectCount{get;}

    /// <summary>
    /// Windows のファイルパスを内部で使用するファイル名 (内部名) に変換します。
    /// </summary>
    /// <param name="lpath">Windows のファイルパスを指定します。</param>
    /// <returns>内部で使用するファイル名を返します。</returns>
    string ResolvePath(string lpath);
    /// <summary>
    /// 指定したディレクトリに含まれるファイルリストを取得します。
    /// </summary>
    /// <param name="path">ディレクトリの内部名を指定します。</param>
    /// <param name="list">ファイルリストを返します。</param>
    /// <returns>成功した場合に 0 を返します。失敗した場合はエラーコードを返します。</returns>
    int GetFileList(string path,out Gen::IEnumerable<FileInfo> list);
    /// <summary>
    /// 指定したファイルの情報を取得します。
    /// </summary>
    /// <param name="rpath">情報を取得するファイルの内部名を指定します。</param>
    /// <returns>指定したファイルが存在しない場合に null を返します。
    /// 指定したファイルが存在した場合にファイル情報を返します。
    /// </returns>
    /// <exception cref="FsBasicReturnException">
    /// 指定したファイルの情報を取得できなかった場合に、
    /// そのエラーコードを保持する FsBasicReturnException を投げます。
    /// </exception>
    FileInfo GetFileInfo(string rpath);
    /// <summary>
    /// ファイルの情報を設定します。
    /// </summary>
    /// <param name="rpath">情報を設定するファイルの内部名を指定します。</param>
    /// <param name="finfo">更新する情報を格納した FileInfo を指定します。</param>
    /// <param name="flags">更新する情報の種類を指定します。</param>
    /// <returns>成功した場合に 0 を返します。それ以外の場合にエラーコードを返します。</returns>
    int SetFileInfo(string rpath,FileInfo finfo,SetFileInfoFlags flags);

    /// <summary>
    /// ディレクトリを作成します。
    /// </summary>
    /// <param name="rpath">作成するディレクトリの内部名を指定します。</param>
    /// <returns>成功した場合に 0 を返します。失敗した場合はエラーコードを返します。</returns>
    int CreateDirectory(string rpath);
    /// <summary>
    /// ディレクトリを削除します。
    /// </summary>
    /// <param name="rpath">削除するディレクトリの内部名を指定します。</param>
    /// <returns>成功した場合に 0 を返します。失敗した場合はエラーコードを返します。</returns>
    int RemoveDirectory(string rpath);
    /// <summary>
    /// 新しく空のファイルを作成します。
    /// 既にファイルが存在していた場合には既存のファイルはなくなります。
    /// </summary>
    /// <param name="rpath">作成するファイルを内部名で指定します。</param>
    /// <returns>成功した場合に 0 を返します。それ以外の場合にエラーコードを返します。</returns>
    int CreateFile(string rpath);
    /// <summary>
    /// ファイルを削除します。
    /// </summary>
    /// <param name="rpath">削除するファイルを内部名で指定します。</param>
    /// <returns>成功した場合に 0 を返します。それ以外の場合にエラーコードを返します。</returns>
    int RemoveFile(string rpath);
    /// <summary>
    /// ファイルまたはディレクトリを指定します。
    /// </summary>
    /// <param name="rpaths">移動元のファイル名を指定します。</param>
    /// <param name="rpathd">移送先のファイル名を指定します。</param>
    /// <returns>成功した場合に 0 を返します。それ以外の場合にエラーコードを返します。</returns>
    int MoveFile(string rpaths,string rpathd);

    void OpenFile(string rpath);
    void CloseFile(string rpath);
    int ReadData(byte[] buff,int buffOffset,string path,long offset,ref int length);
    int WriteData(byte[] buff,int buffOffset,string path,long offset,ref int length);
  }

  class GvfsOperation:DokanOperations{
    readonly IFsBasic basic;

    private FileAutoCloser closer;
    private SshfsMessage output;

    public GvfsOperation(IFsBasic basic,mwg.Sshfs.SshfsMessage output){
      this.output=output;

      this.basic=new CachedFsBasic(basic);
      this.closer=new FileAutoCloser(this.basic);
    }
    public GvfsOperation(ISshSession session,ISftpAccount account)
      :this(new SftpFsBasic(session,account),session.Message)
    {}
    public GvfsOperation(SshUserData userdata,ISftpAccount account)
      :this(new SshSession(userdata,account),account)
    {}
    //==========================================================================
    //  Dokan.SSHFS ShellEx
    //--------------------------------------------------------------------------
    int DokanOperations.GetDiskFreeSpace(ref ulong freeBytesAvailable,ref ulong totalBytes,ref ulong totalFreeBytes,DokanFileInfo info) {
#if LOG_QUERY
      output.Write(2,"i GetDiskFreeSpace()");
#endif
      const long GIGA=1024*1024*1024;
      freeBytesAvailable  =1024*GIGA;
      totalBytes          =2048*GIGA;
      totalFreeBytes      =1024*GIGA;
      return 0;
    }

    int DokanOperations.Unmount(DokanFileInfo info) {
#if LOG_QUERY
      output.Write(2,"i Unmount()");
#endif
      if(this.closer!=null){
        this.closer.Dispose();
        this.closer=null;
      }
      try{
        this.basic.Disconnect();
        this.basic.Dispose();
      }catch{}

      return 0;
    }

    //==========================================================================
    //  ディレクトリ操作
    //==========================================================================
    int InternalCreateDirectory(string lpath,DokanFileInfo info){
      if(basic.IsReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
      try{
        string rpath=basic.ResolvePath(lpath);
        FileInfo finfo=basic.GetFileInfo(rpath);
        if(finfo!=null&&finfo.IsDirectory)return 0;

        return basic.CreateDirectory(rpath);
      }catch(System.Exception e1){
        return this.ProcessException(e1);
      }
    }
    //--------------------------------------------------------------------------
    int InternalDeleteDirectory(string lpath, DokanFileInfo info){
      if(basic.IsReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
      try{
        string rpath=this.basic.ResolvePath(lpath);

        return basic.RemoveDirectory(rpath);
      }catch(System.Exception e1){
        return this.ProcessException(e1);
      }
    }
    //--------------------------------------------------------------------------
    int InternalOpenDirectory(string lpath, DokanFileInfo info){
      try{
        string rpath=basic.ResolvePath(lpath);
        FileInfo finfo=basic.GetFileInfo(rpath);
        if(finfo!=null&&finfo.IsDirectory)return 0;
      }catch(System.Exception e1){
        int ret=this.ProcessException(e1);
        if(ret==1)return 1;
      }
      return -WinErrorCode.ERROR_PATH_NOT_FOUND; // 何故決め打ち?
    }
    //==========================================================================
    /// <summary>
    /// 一定時間が経過したら自動的にファイルを閉じる操作を実行します。
    /// </summary>
    class FileAutoCloser:System.IDisposable{
      static System.TimeSpan TIME_CLOSEFILE=new System.TimeSpan(0,5,0);

      IFsBasic basic;
      //FileCacheControl cache;
      Gen::Dictionary<string,System.DateTime> times
        =new Gen::Dictionary<string,System.DateTime>();

      public FileAutoCloser(IFsBasic basic){
        this.basic=basic;
        Sshfs.Program.Background+=this.bg_close;
      }
      //public FileAutoCloser(FileCacheControl cache){
      //  this.cache=cache;
      //  Sshfs.Program.Background+=this.bg_close;
      //}
      public void Dispose(){
        Sshfs.Program.Background-=this.bg_close;
        lock(times){
          foreach(string path in times.Keys){
            try{basic.CloseFile(path);}catch{}
          }
          times.Clear();
        }
      }
      public void Touch(string path){
        lock(times){
          if(!times.ContainsKey(path))basic.OpenFile(path);
          times[path]=System.DateTime.Now;
        }
      }
      public bool CloseNow(string path){
        lock(times){
          if(!times.ContainsKey(path))return false;
          times.Remove(path);
        }
        basic.CloseFile(path);
        return true;
      }
      // auto close
      void bg_close(){
        Gen::List<string> closelist=new System.Collections.Generic.List<string>();

        System.DateTime dt_th=System.DateTime.Now-TIME_CLOSEFILE;
        lock(times){
          foreach(Gen::KeyValuePair<string,System.DateTime> pair in times){
            if(pair.Value<dt_th)closelist.Add(pair.Key);
          }
          foreach(string key in closelist){
            basic.CloseFile(key);
            times.Remove(key);
          }
        }
      }
    }

    //==========================================================================
    //  ファイル読み書き
    //==========================================================================
    int InternalCreateFile(string lpath,System.IO.FileAccess acc,System.IO.FileShare share,System.IO.FileMode mode,System.IO.FileOptions options,DokanFileInfo info){
      try{
        AdsPath adsid;
        if(DetectAdsPath(lpath,out adsid)){
          _todo.CacheFsBasicAdsSupport("ads の種類毎に正しく処理したい");
          string rpath_=basic.ResolvePath(adsid.path);
          FileInfo finfo_=basic.GetFileInfo(rpath_);
          if(finfo_==null)
            return -WinErrorCode.ERROR_FILE_NOT_FOUND;
          else
            return 0;
        }

        if(IsAltStream(lpath))return 0;
        string rpath=this.basic.ResolvePath(lpath);

        FileInfo finfo=basic.GetFileInfo(rpath);
        if(finfo!=null&&finfo.IsDirectory)
          info.IsDirectory=true;
        /* cf. (引用 dokan/readme.ja.txt)
         * -------------------------------------------------------------------------
         * > ディレクトリに対するアクセスで有るのにも関わらず， CreateFile が呼ばれ
         * > ることもあります．その場合は， DokanFileInfo->IsDirectory は FALSE がセ
         * > ットされています．ディレクトリの属性を取得する場合などに OpenDirectory 
         * > ではなく，CreateFileが呼ばれるようです．ディレクトリに対するアクセスな
         * > のにも関わらず，CreateFile が呼ばれた場合は，必ず DokanFileInfo->
         * > IsDirectory にTRUEをセットしてから returnしてください．正しくセットされ
         * > ていないと，Dokanライブラリは，そのアクセスがディレクトリに対するアクセ
         * > スかどうか判断できず，Dokanファイルシステムで Windows に対して正確な情
         * > 報を返すことが出来なくなります．
         * -------------------------------------------------------------------------
         */

        int returnCode=0;
        /* cf. (引用 dokan/readme.ja.txt)
         * -------------------------------------------------------------------------
         * > CreateFile で CreationDisposition が CREATE_ALWAYS もしくは OPEN_ALWAYS
         * > の場合で，ファイルがすでに存在していた場合は，0ではなく，
         * > ERROR_ALREADY_EXISTS(183) (正の値) を返してください．
         * -------------------------------------------------------------------------
         * cf (dokan_net/DokanNet/Proxy.cs)
         * -------------------------------------------------------------------------
         * > switch (rawCreationDisposition)
         * > {
         * >   case CREATE_NEW:
         * >     mode = FileMode.CreateNew;
         * >     break;
         * >   case CREATE_ALWAYS:
         * >     mode = FileMode.Create;
         * >     break;
         * >   case OPEN_EXISTING:
         * >     mode = FileMode.Open;
         * >     break;
         * >   case OPEN_ALWAYS:
         * >     mode = FileMode.OpenOrCreate;
         * >     break;
         * >   case TRUNCATE_EXISTING:
         * >     mode = FileMode.Truncate;
         * >     break;
         * > }
         * -------------------------------------------------------------------------
         */

        switch(mode){
          case System.IO.FileMode.CreateNew:
            if(finfo!=null)
              return -WinErrorCode.ERROR_ALREADY_EXISTS;
            goto clear_file;
          case System.IO.FileMode.Create: // CREATE_ALWAYS
            if(finfo!=null)
              returnCode=+WinErrorCode.ERROR_ALREADY_EXISTS;
            goto clear_file;
          case System.IO.FileMode.Open:
            if(finfo==null)
              return -WinErrorCode.ERROR_FILE_NOT_FOUND;
            break;
          case System.IO.FileMode.OpenOrCreate: // OPEN_ALWAYS
            if(finfo==null)
              goto clear_file;
            else
              returnCode=+WinErrorCode.ERROR_ALREADY_EXISTS;
            break;
          case System.IO.FileMode.Truncate:
            if(finfo==null)
              return -WinErrorCode.ERROR_FILE_NOT_FOUND;
            goto clear_file;
          case System.IO.FileMode.Append:
            if(finfo==null)
              goto clear_file;
            break;
          default:
            this.output.Write(0,"!! ERR !! unknown FileMode {0}!",mode);
            return -1;
          clear_file:
            if(finfo!=null){
              if(finfo.IsDirectory||finfo.Length==0)
                break;
              else if(this.basic.IsReadOnly)
                return -WinErrorCode.ERROR_WRITE_PROTECT;
              else if(finfo.IsReadOnly)
                return -WinErrorCode.ERROR_FILE_READ_ONLY;

              finfo.Length=0;
              this.basic.SetFileInfo(rpath,finfo,SetFileInfoFlags.SetFileSize);
            }else{
              if(this.basic.IsReadOnly)
                return -WinErrorCode.ERROR_WRITE_PROTECT;

              int r=this.basic.CreateFile(rpath);
              if(r!=0)return r;
            }
            break;
        }

        //this.cache.Open(path);
        this.closer.Touch(rpath);
        /* -------------------------------------------------------------------------
         * Dokan では open/close がちゃんと管理されていない?
         * -------------------------------------------------------------------------
         * CreateFile の直後に Cleanup/CloseFile が呼ばれる。更に、その後に読み出し
         * や書込が実行されて、終わっても CloseFile 等は呼び出されない。読み書きのキ
         * ャッシュは CloseFile/Cleanup とは独立に管理する事にする。
         * -------------------------------------------------------------------------
         * cf. (from dokan/readme.ja.txt)
         * -------------------------------------------------------------------------
         * > 【注意】ユーザがファイルをメモリマップドファイルとして開いている場合，
         * > Cleanup が呼ばれた後に WriteFile や ReadFile が呼ばれる場合があります．
         * > この場合にも正常に読み込み書き込みができるようにするべきです．
         * -------------------------------------------------------------------------
         */

        return returnCode;
      }catch(System.Exception e1){
        return this.ProcessException(e1);
      }
    }
    //--------------------------------------------------------------------------
    int DokanOperations.FlushFileBuffers(string filename, DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i FlushFileBuffers({0})",filename);
#endif
      return 0;
    }
    int DokanOperations.Cleanup(string filename,DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i Cleanup({0})",filename);
#endif
      return 0;
    }
    int DokanOperations.CloseFile(string filename,DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i CloseFile({0})",filename);
#endif
      return 0;
    }
    //--------------------------------------------------------------------------
    int InternalReadFile(string lpath,byte[] buffer,ref uint readBytes,long offset,DokanFileInfo info){
      try{
        if(lpath.EndsWith("\\")){
          // Cygwin ls などで、通常のファイル名の末端に \\ をつけて ReadFile が呼び出される…。
          return -WinErrorCode.ERROR_INVALID_NAME;
        }

        AdsPath adsid;
        if(DetectAdsPath(lpath,out adsid)){
          int code=AdsReadFile(adsid,offset,buffer,ref readBytes);
          if(code!=FsBasicUtil.ErrorUnknownAlternateData)return code;
        }

        string rpath=this.basic.ResolvePath(lpath);

        //if(info.IsDirectory)return -1;
        FileInfo finfo=basic.GetFileInfo(rpath);
        if(finfo!=null&&finfo.IsDirectory)return -1;
        /* -----------------------------------------
         * 何故か Directory として開いたのにも拘わらず
         * info.IsDirectory の値が勝手に変わってしまう事がある様だ。
         * 従って、info.IsDirectory ではなく、finfo.IsDirectory を用いる必要がある。
         * -----------------------------------------
         */

        this.closer.Touch(rpath);
        int length=buffer.Length;
        int ret=this.basic.ReadData(buffer,0,rpath,offset,ref length);
        readBytes=(uint)length;
        return ret;
      }catch(System.Exception e1){
        return this.ProcessException(e1);
      }
    }
    int InternalWriteFile(string lpath,byte[] buffer,ref uint writtenBytes,long offset,DokanFileInfo info){
      try{
        if(basic.IsReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;

        AdsPath adsid;
        if(DetectAdsPath(lpath,out adsid)){
          int code=AdsWriteFile(adsid,offset,buffer,ref writtenBytes);
          if(code==FsBasicUtil.ErrorUnknownAlternateData)
            return -WinErrorCode.ERROR_FILE_READ_ONLY;
          else
            return code;
        }

        string rpath=basic.ResolvePath(lpath);

        // check
        FileInfo finfo=basic.GetFileInfo(rpath);
        if(finfo!=null&&finfo.IsReadOnly)
          return -WinErrorCode.ERROR_FILE_READ_ONLY;

        if(offset<0)
          offset=finfo!=null?finfo.Length:0;

        this.closer.Touch(rpath);
        int length=buffer.Length;
        int returnCode=basic.WriteData(buffer,0,rpath,offset,ref length);
        writtenBytes=(uint)length;
        return returnCode;
      }catch(System.Exception e1){
        return this.ProcessException(e1);
      }
    }
    //--------------------------------------------------------------------------
    struct AdsPath{
      public string path;
      public string name;
      public string type;
    }
    static bool IsAltStream(string filename){
      if(filename.IndexOf(':')<0)return false;
      string[] arr=filename.Split(new char[]{':'}, 2);
      return arr.Length==2&&arr[1].StartsWith("SSHFSProperty.");
    }

    static char[] PathSeparaters={'\\','/'};
    bool DetectAdsPath(string lpath,out AdsPath adsid){
      int nameIndex=1+lpath.LastIndexOfAny(PathSeparaters);

      int index1=lpath.LastIndexOf(':',lpath.Length-1,lpath.Length-nameIndex);
      if(index1<=0){
        adsid.path=null;
        adsid.type=null;
        adsid.name=null;
        return false;
      }

      int index2=lpath.LastIndexOf(':',index1-1,index1-nameIndex);
      if(index2<0){
        adsid.path=lpath.Substring(0,index1);
        adsid.name=lpath.Substring(index1+1);
        adsid.type=null;
        return true;
      }else{
        adsid.path=lpath.Substring(0,index2);
        adsid.name=lpath.Substring(index2+1,index1-index2-1);
        adsid.type=lpath.Substring(index1+1);
        return true;
      }
    }
    int AdsWriteFile(AdsPath adsid,long offset,byte[] buffer,ref uint writtenBytes){
      string rpath=basic.ResolvePath(adsid.path);
      if(adsid.name=="SSHFSProperty.Permission"){
        if(offset==0){
          string s_permission=System.Text.Encoding.ASCII.GetString(buffer);
          this.SetPermission(rpath,System.Convert.ToInt32(s_permission,8));
          writtenBytes=(uint)buffer.Length;
        }else
          writtenBytes=0;
        return 0;
      }else{
        output.Write(1,"! WriteAds: unrecognized {0}:{1}:{2}",adsid.path,adsid.name,adsid.type??"");
        return FsBasicUtil.ErrorUnknownAlternateData;
      }
    }
    int AdsReadFile(AdsPath adsid,long offset,byte[] buffer,ref uint readBytes){
      string rpath=basic.ResolvePath(adsid.path);
      switch(adsid.name){
        case "SSHFSProperty.Permission":
          if(offset==0){
            string s=this.GetPermission(rpath);
            readBytes=(uint)System.Text.Encoding.ASCII.GetBytes(s,0,s.Length,buffer,0);
          }else
            readBytes=0;
          return 0;
        case "Zone.Identifier":
          int length=buffer.Length;
          int code=FsBasicUtil.ReadDataFromArray(GetZoneId(3),offset,buffer,0,ref length);
          readBytes=(uint)length;
          return code;
        default:
          output.Write(1,"! ReadAds: unrecognized {0}:{1}:{2}",adsid.path,adsid.name,adsid.type??"");
          return FsBasicUtil.ErrorUnknownAlternateData;
      }
    }
    int AdsGetFileInfo(AdsPath adsid,ref FileInfo finfo){
      string rpath=finfo.InternalPath;
      switch(adsid.name){
        case "SSHFSProperty.Permission":
          finfo=new FileInfo(adsid.path+":"+adsid.name,finfo.Name+":"+adsid.name,finfo);
          string s=this.GetPermission(rpath);
          finfo.Length=System.Text.Encoding.ASCII.GetByteCount(s);
          return 0;
        case "Zone.Identifier":
          byte[] data=GetZoneId(3);
          if(data==null)
            return -WinErrorCode.ERROR_FILE_NOT_FOUND;
          else{
            finfo=new FileInfo(adsid.path+":"+adsid.name,finfo.Name+":"+adsid.name,finfo);
            finfo.Length=data.Length;
            finfo.Attributes&=~System.IO.FileAttributes.Directory;
            return 0;
          }
        default:
          output.Write(1,"! ReadAds: unrecognized {0}:{1}:{2}",adsid.path,adsid.name,adsid.type??"");
          return FsBasicUtil.ErrorUnknownAlternateData;
      }
    }
    string GetPermission(string path){
      try{
        FileInfo finfo=basic.GetFileInfo(path);
        if(finfo==null){
          this.output.Write(1,"! GetPermission: file '{0}' not found",path);
          return "0\n";
        }

        int perm=finfo.Permission&0xFFF;
        return System.Convert.ToString(perm,8)+"\n";
      }catch(System.Exception e1){
        this.ProcessException(e1);
        return "0\n";
      }
    }
    bool SetPermission(string path,int permission){
      try{
        FileInfo finfo=basic.GetFileInfo(path);
        if(finfo==null){
          this.output.Write(1,"! SetPermission: file '{0}' not found",path);
          return false;
        }

        finfo.Permission=permission;
        this.basic.SetFileInfo(path,finfo,SetFileInfoFlags.SetPermission);
        return true;
      }catch(System.Exception e1){
        this.ProcessException(e1);
        return false;
      }
    }
    static byte[] GetZoneId_Buffer
      =System.Text.Encoding.ASCII.GetBytes("[ZoneTransfer]\r\nZoneId=3\r\n");
    //                                      012345678901234 5 678901234 5 
    static byte[] GetZoneId(int id){
      if(id==0)return null;
      if(id<0||3<id)id=3;
      GetZoneId_Buffer[23]=(byte)('0'+id);
      return GetZoneId_Buffer;
    }
    //--------------------------------------------------------------------------
    int InternalSetAllocationSize(string lpath,long length,DokanFileInfo info){
      if(basic.IsReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
      try{
        string rpath=basic.ResolvePath(lpath);
        FileInfo finfo=basic.GetFileInfo(rpath);
        if(finfo==null){
          output.Write(1,"! SetAllocationSize: file '{0}' not found",rpath);
          return -WinErrorCode.ERROR_FILE_NOT_FOUND;
        }

        if(finfo.Length<length){
          finfo.Length=length;
          basic.SetFileInfo(rpath,finfo,SetFileInfoFlags.SetFileSize);
        }

        return 0;
      }catch(System.Exception e1){
        return ProcessException(e1);
      }
    }
    //--------------------------------------------------------------------------
    int InternalSetEndOfFile(string lpath, long length, DokanFileInfo info){
      if(basic.IsReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
      try{
        string rpath=basic.ResolvePath(lpath);
        FileInfo finfo=basic.GetFileInfo(rpath);
        if(finfo==null){
          output.Write(1,"! SetEndOfFile: file '{0}' not found",rpath);
          return -WinErrorCode.ERROR_FILE_NOT_FOUND;
        }

        finfo.Length=length;
        basic.SetFileInfo(rpath,finfo,SetFileInfoFlags.SetFileSize);
        return 0;
      }catch(System.Exception e1){
        return ProcessException(e1);
      }
    }
    //==========================================================================
    //  ファイル情報
    //==========================================================================
    private void CopyFileInfo(Dokan.FileInformation info,FileInfo finfo){
      info.Length=finfo.Length;
      info.CreationTime=finfo.CreationTime;
      info.LastAccessTime=finfo.LastAccessTime;
      info.LastWriteTime=finfo.LastWriteTime;

      info.Attributes=
        finfo.IsDirectory?System.IO.FileAttributes.Directory:
        System.IO.FileAttributes.Normal;

      const System.IO.FileAttributes MASK_ATTR=
        System.IO.FileAttributes.Offline|
        System.IO.FileAttributes.ReadOnly|
        System.IO.FileAttributes.Hidden|
        System.IO.FileAttributes.System;

      info.Attributes|=finfo.Attributes&MASK_ATTR;
      if(basic.IsReadOnly)
        info.Attributes|=System.IO.FileAttributes.ReadOnly;
      if(finfo.IsDirectory)
        info.Attributes&=~System.IO.FileAttributes.ReadOnly;
    }
    //--------------------------------------------------------------------------
    int InternalGetFileInformation(string lpath, FileInformation fileinfo, DokanFileInfo info){
      try{
        FileInfo finfo;

        AdsPath adsid;
        if(DetectAdsPath(lpath,out adsid)){
          string rpath=basic.ResolvePath(adsid.path);
          finfo=basic.GetFileInfo(rpath);
          if(finfo==null)
            return -WinErrorCode.ERROR_FILE_NOT_FOUND;

          int code=AdsGetFileInfo(adsid,ref finfo);
          if(code==FsBasicUtil.ErrorUnknownAlternateData)
            return -WinErrorCode.ERROR_FILE_READ_ONLY;
          else if(code!=0)
            return code;
        }else{
          string rpath=basic.ResolvePath(lpath);
          finfo=basic.GetFileInfo(rpath);
          if(finfo==null){
            output.Write(1,"! GetFileInformation: file '{0}' not found.",lpath);
            return -WinErrorCode.ERROR_FILE_NOT_FOUND;
          }
        }

        fileinfo.FileName=System.IO.Path.GetFileName(lpath);
        this.CopyFileInfo(fileinfo,finfo);
        return 0;
      }catch(System.Exception e1){
        return ProcessException(e1);
      }
    }
    //--------------------------------------------------------------------------
    int InternalFindFiles(string lpath,System.Collections.ArrayList files,DokanFileInfo info){
      try{
        string rpath=basic.ResolvePath(lpath);
        Gen::IEnumerable<FileInfo> flist;
        int code=basic.GetFileList(rpath,out flist);
        //if(code==-WinErrorCode.ERROR_FILE_NOT_FOUND)
        //  output.Write(1,"! FindFiles: directory {0} not found",rpath);
        if(code!=0)return code;

        foreach(FileInfo finfo in flist){
          FileInformation fileinfo=new FileInformation();
          fileinfo.FileName=finfo.Name;
          this.CopyFileInfo(fileinfo,finfo);
          files.Add(fileinfo);
        }
        return 0;
      }catch(System.Exception e1){
        files.Clear();
        return ProcessException(e1);
      }
    }
    //--------------------------------------------------------------------------
    int InternalSetFileAttributes(string lpath,System.IO.FileAttributes _attr, DokanFileInfo info){
      if(basic.IsReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
      try{
        string rpath=basic.ResolvePath(lpath);
        FileInfo finfo=basic.GetFileInfo(rpath);
        if(finfo==null){
          output.Write(1,"! SetFileAttribute: file '{0}' not found.",rpath);
          return -WinErrorCode.ERROR_FILE_NOT_FOUND;
        }

        //■TODO: 属性変更は可能か? 可能なら実装する
        //  抑もどの様な属性が存在するのか??
        //jsch::SftpATTRS attr=this.stat(rpath);
        //int permissions=attr.getPermissions();
        //attr.setPERMISSIONS(permissions);
        //this.setStat(rpath,attr);
        return 0;
      }catch(System.Exception e1){
        return ProcessException(e1);
      }
    }
    //--------------------------------------------------------------------------
    int InternalSetFileTime(string filename,System.DateTime ctime,System.DateTime atime,System.DateTime mtime,DokanFileInfo info){
      if(basic.IsReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
      try{
        string rpath=basic.ResolvePath(filename);
        FileInfo finfo=basic.GetFileInfo(rpath);
        if(finfo==null){
          output.Write(1,"! SetFileTime: file '{0}' not found.",rpath);
          return -WinErrorCode.ERROR_FILE_NOT_FOUND;
        }

        if(ctime!=System.DateTime.MinValue)
          finfo.CreationTime=ctime;
        if(atime!=System.DateTime.MinValue)
          finfo.LastAccessTime=atime;
        if(mtime!=System.DateTime.MinValue)
          finfo.LastWriteTime=mtime;

        basic.SetFileInfo(rpath,finfo,SetFileInfoFlags.SetFileTime);
        return 0;
      }catch(System.Exception e1){
        return ProcessException(e1);
      }
    }
    //==========================================================================
    //  ノード操作
    //==========================================================================
    int InternalDeleteFile(string filename, DokanFileInfo info){
      if(basic.IsReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
      try{
        string rpath=basic.ResolvePath(filename);
        closer.CloseNow(rpath);
        return basic.RemoveFile(rpath);
      }catch(System.Exception e1){
        return ProcessException(e1);
      }
    }
    //--------------------------------------------------------------------------
    int DokanOperations.LockFile(string filename, long offset, long length, DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i LockFile({0})",filename);
#endif
      return 0;
    }
    int DokanOperations.UnlockFile(string filename,long offset,long length,DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i UnlockFile({0})",filename);
#endif
      return 0;
    }
    //--------------------------------------------------------------------------
    int InternalMoveFile(string lpaths,string lpathd,bool replace,DokanFileInfo info){
      if(basic.IsReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
      try{
        string rpaths=basic.ResolvePath(lpaths);
        string rpathd=basic.ResolvePath(lpathd);

        // check
        FileInfo finfos=basic.GetFileInfo(rpaths);
        FileInfo finfod=basic.GetFileInfo(rpathd);
        if(finfos==null){
          output.Write(1,"! MoveFile: source file '{0}' not found.",rpaths);
          return -WinErrorCode.ERROR_FILE_NOT_FOUND;
        }
        if(finfod!=null&&(!replace||finfod.IsDirectory)){
          return -WinErrorCode.ERROR_ALREADY_EXISTS;
        }

        closer.CloseNow(rpaths);
        closer.CloseNow(rpathd);
        return basic.MoveFile(rpaths,rpathd);
      }catch(System.Exception e){
        return ProcessException(e);
      }
    }

    //==========================================================================
    // [gvfs.errorhandle]
    //--------------------------------------------------------------------------
    [Compiler::MethodImpl(Compiler::MethodImplOptions.NoInlining)]
    int ProcessException(System.Exception e){
      FsBasicReturnException e1=e as FsBasicReturnException;
      if(e1!=null)return e1.returnCode;

      Diag::StackFrame callerFrame=new Diag::StackFrame(1);
      Ref::MethodBase caller=callerFrame.GetMethod();
      if(e is FailedToEstablishChannelException){
        output.Write(0,"! Failed to establish a channel @ {0}",caller.Name);
      }else{
        output.Write(0,@"! {0}: caught exception
----- ----- ----- ----- -----
{1}
-----------------------------",caller.Name,e);
      }
      return FsBasicUtil.ErrorRequestReconnection;
    }
    #region DokanOperations メンバ 自動生成
    int DokanOperations.CreateDirectory(string filename,DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i CreateDirectory({0})",filename);
#endif
      int ret=InternalCreateDirectory(filename,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalCreateDirectory(filename,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.DeleteDirectory(string filename, DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i DeleteDirectory({0})",filename);
#endif
      int ret=InternalDeleteDirectory(filename,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalDeleteDirectory(filename,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.OpenDirectory(string filename, DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i OpenDirectory({0})",filename);
#endif
      int ret=InternalOpenDirectory(filename,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalOpenDirectory(filename,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.CreateFile(string filename,System.IO.FileAccess acc,System.IO.FileShare share,System.IO.FileMode mode, System.IO.FileOptions options, DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i CreateFile({0})",filename);
#endif
      int ret=InternalCreateFile(filename,acc,share,mode,options,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalCreateFile(filename,acc,share,mode,options,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.ReadFile(string filename,byte[] buffer,ref uint readBytes,long offset,DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i ReadFile({0})",filename);
#endif
      int ret=InternalReadFile(filename,buffer,ref readBytes,offset,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalReadFile(filename,buffer,ref readBytes,offset,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.WriteFile(string filename, byte[] buffer, ref uint writtenBytes, long offset, DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i WriteFile({0})",filename);
#endif
      int ret=InternalWriteFile(filename,buffer,ref writtenBytes,offset,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalWriteFile(filename,buffer,ref writtenBytes,offset,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.SetAllocationSize(string filename, long length,DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i SetAllocationSize({0})",filename);
#endif
      int ret=InternalSetAllocationSize(filename,length,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalSetAllocationSize(filename,length,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.SetEndOfFile(string filename, long length, DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i SetEndOfFile({0})",filename);
#endif
      int ret=InternalSetEndOfFile(filename,length,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalSetEndOfFile(filename,length,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.GetFileInformation(string filename, FileInformation fileinfo, DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i GetFileInformation({0})",filename);
#endif
      int ret=InternalGetFileInformation(filename,fileinfo,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalGetFileInformation(filename,fileinfo,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.FindFiles(string filename,System.Collections.ArrayList files,DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i FindFiles({0})",filename);
#endif
      int ret=InternalFindFiles(filename,files,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalFindFiles(filename,files,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.SetFileAttributes(string filename, System.IO.FileAttributes _attr, DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i SetFileAttributes({0})",filename);
#endif
      int ret=InternalSetFileAttributes(filename,_attr,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalSetFileAttributes(filename,_attr,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.SetFileTime(string filename,System.DateTime ctime,System.DateTime atime,System.DateTime mtime,DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i SetFileTime({0})",filename);
#endif
      int ret=InternalSetFileTime(filename,ctime,atime,mtime,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalSetFileTime(filename,ctime,atime,mtime,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.DeleteFile(string filename, DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i DeleteFile({0})",filename);
#endif
      int ret=InternalDeleteFile(filename,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalDeleteFile(filename,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    int DokanOperations.MoveFile(string filename,string newname,bool replace,DokanFileInfo info){
#if LOG_QUERY
      output.Write(2,"i MoveFile({0},{1})",filename,newname);
#endif
      int ret=InternalMoveFile(filename,newname,replace,info);
      if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;

      int c=basic.ReconnectCount;
      while(c--!=0){
        if(!basic.Reconnect())continue;
        ret=InternalMoveFile(filename,newname,replace,info);
        if(ret!=FsBasicUtil.ErrorRequestReconnection)return ret;
      }
      return -1;
    }
    #endregion
  }

}

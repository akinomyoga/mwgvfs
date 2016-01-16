using Dokan;
using jsch=Tamir.SharpSsh.jsch;
using java=Tamir.SharpSsh.java;
using Gen=System.Collections.Generic;
using Forms=System.Windows.Forms;

namespace mwg.mwgvfs.gvfs{

  enum SftpFileType{
    Directory,
    NormalFile,
    //LinkAsNormalFile,
    LinkAsCygwinSymlink,
    LinkAsDereferenced,
  }

  class SftpFileInfo:FileInfo{
    SftpFsBasic basic;

    public SftpFileInfo(
      System.DateTime informationTime,
      SftpFsBasic basic,
      string rpath)
      :base(rpath,Unix.UnixPath.GetWFileName(rpath),informationTime)
    {
      this.basic=basic;
    }

    Tamir.SharpSsh.jsch.SftpATTRS attrs;
    public Tamir.SharpSsh.jsch.SftpATTRS JschAttribute{
      get{return attrs;}
      set{attrs=value;}
    }

    SftpFileType ftype=SftpFileType.NormalFile;
    public SftpFileType FileType{
      get{return this.ftype;}
      set{this.ftype=value;}
    }

    public override long Length{
      get{
        if(this.FileType==SftpFileType.LinkAsCygwinSymlink)
          return basic.LinkGetCyglinkData(this).Length;
        return base.Length;
      }
      set{base.Length=value;}
    }
  }

  class SftpFsBasic:IFsBasic{
    // Contents
    //   sftp.finfo
    //   sftp.finfo.download
    //   sftp.finfo.cache
    //   sftp.finfo.public
    //   sftp.change
    //   sftp.readwrite
    //   sftp.readwrite.read
    //   sftp.readwrite.write
    //   sftp.errorhandle

    mwg.Sshfs.ISftpAccount account;
    mwg.Sshfs.ISshSession session;

    readonly FileInfoCache ficache;

    private string rpath_base;
    public mwg.Sshfs.ISshSession Session{
      get{return this.session;}
    }
    public string RootDirectory{
      get{return this.rpath_base;}
    }
    //------------------------------------------------------------------------

    public SftpFsBasic(mwg.Sshfs.ISshSession session,mwg.Sshfs.ISftpAccount account){
      this.session=session;
      this.account=account;

      this.ficache=new FileInfoCache();

      // resolve_path
      string rpath_base=account.ServerRoot;
      if(rpath_base==null)rpath_base="";
      if(rpath_base[rpath_base.Length-1]=='/')
        rpath_base=rpath_base.Substring(0,rpath_base.Length-1);
      this.rpath_base=rpath_base;
    }

    public bool IsReadOnly{
      get{return this.account.ReadOnly;}
    }
    public int ReconnectCount {
      get { return this.account.ReconnectCount; }
    }
    public bool Reconnect() {
      return this.session.Reconnect();
    }
    public void Disconnect(){
      session.Disconnect();
    }
    public void Dispose(){}

    string GetParentDirectoryPath(string rpath){
      if(rpath.Length>this.rpath_base.Length+1)
        return Unix.UnixPath.GetParentPath(rpath);
      return rpath;
    }
    /// <summary>
    /// ローカルのファイルパスをリモートのファイルパスに変換します。
    /// </summary>
    /// <param name="filename">ローカルのファイルパスを指定します。</param>
    /// <returns>リモートのファイルパスを指定します。</returns>
    public string ResolvePath(string lpath){
      lpath=Unix.UnixPath.ConvertWPathToUPath(lpath);
      if(lpath.Length==0||lpath[0]!='/')
        return this.rpath_base+"/"+lpath;
      else
        return this.rpath_base+lpath;
    }

    #region FileInfo 取得・管理
    //==========================================================================
    //  [sftp.finfo] FileInfo 取得・管理
    //------------------------------------------------------------------------
    //  [sftp.finfo.download] FileInfo 取得
    FileInfo CreateFileInfo(string rpath,jsch::SftpATTRS attrs,System.DateTime infotime){
      SftpFileInfo info=new SftpFileInfo(infotime,this,rpath);
      info.JschAttribute=attrs;
      info.FileType=SftpFileType.NormalFile;

      if(attrs.isDir()){
        info.Attributes|=System.IO.FileAttributes.Directory;
        info.FileType=SftpFileType.Directory;
      }else if(attrs.isLink()){
        switch(account.SymlinkTreatment){
          case mwg.Sshfs.SftpSymlink.Dereference:
            info.FileType=SftpFileType.LinkAsDereferenced;
            info.Attributes|=System.IO.FileAttributes.ReparsePoint;
            break;
          case mwg.Sshfs.SftpSymlink.NormalFile:
          case mwg.Sshfs.SftpSymlink.Shortcut:
            info.FileType=SftpFileType.LinkAsCygwinSymlink;
            info.Attributes|=System.IO.FileAttributes.ReadOnly;
            info.Attributes|=System.IO.FileAttributes.System;
            break;
        }
      }

      if(this.IsReadOnly&&!info.IsDirectory)
        info.Attributes|=System.IO.FileAttributes.ReadOnly;
      if(account.Offline)
        info.Attributes|=System.IO.FileAttributes.Offline;
      if(info.InternalName.Length>0&&info.InternalName[0]=='.')
        info.Attributes|=System.IO.FileAttributes.Hidden;

      info.Permission=attrs.getPermissions();
      info.LastWriteTime=Unix.UnixTime.UnixTimeToDateTime(attrs.getMTime());
      info.LastAccessTime=Unix.UnixTime.UnixTimeToDateTime(attrs.getATime());
      info.CreationTime=info.LastWriteTime;
      info.Length=attrs.getSize();

      return info;
    }

    /// <summary>
    /// path で指定したディレクトリの内容を読み取って、情報を更新します。
    /// </summary>
    /// <param name="rpath">目的のディレクトリを示すパスを指定します。</param>
    /// <exception cref="Tamir.SharpSsh.jsch.SftpException">
    /// 読み取りに失敗した場合に発生します。</exception>
    Gen::List<FileInfo> UpdateList(string rpath){
      // 情報取得
      session.Message.Write(1,"$ ls {0}",rpath);
      java::util.Vector entries;
      try{
        entries=session.Sftp.noglob_ls(rpath);
      }catch(Tamir.SharpSsh.jsch.SftpException e){
        switch((Unix.SSH_ERROR)e.id){
          case mwg.Unix.SSH_ERROR.NO_SUCH_FILE:
          case mwg.Unix.SSH_ERROR.NO_SUCH_PATH:
            session.Message.Write(1,"! ls: not found {0}",rpath);
            ficache.SetList(rpath,null); // "存在しない"
            return null;
          default:
            throw;
        }
      }

      // 結果登録
      string dir=rpath;
      if(!dir.EndsWith("/"))dir+="/";

      System.DateTime now=System.DateTime.Now;
      Gen::List<FileInfo> list=new Gen::List<FileInfo>();
      foreach(Tamir.SharpSsh.jsch.ChannelSftp.LsEntry entry in entries){
        //string filename=mwg.Unix.UnixPath.QuoteWildcard(entry.getFilename());
        string filename=entry.getFilename();
        if(filename=="."){
          string filepath=rpath;
          FileInfo info=this.CreateFileInfo(filepath,entry.getAttrs(),now);
          this.ficache.SetFile(info);
        }else if(filename==".."){
          continue;
        }else{
          string filepath=dir+filename;
          FileInfo info=this.CreateFileInfo(filepath,entry.getAttrs(),now);
          list.Add(info);
        }
      }
      this.ficache.SetList(rpath,list);
      return list;
    }

    FileInfo UpdateFile(string rpath){
      try{
        session.Message.Write(1,"$ stat {0}",rpath);
        jsch::SftpATTRS attrs=session.Sftp.noglob_stat(rpath);
        FileInfo info=this.CreateFileInfo(rpath,attrs,System.DateTime.Now);
        this.ficache.SetFile(info);
        return info;
      }catch(Tamir.SharpSsh.jsch.SftpException e){
        switch((Unix.SSH_ERROR)e.id){
          // ファイルが見付からなかった場合: null を返す。
          case mwg.Unix.SSH_ERROR.NO_SUCH_FILE:
          case mwg.Unix.SSH_ERROR.NO_SUCH_PATH:
            session.Message.Write(1,"! stat: not found {0}",rpath);
            return null;
          default:
          // その他の問題でエラーになった場合: throw
            throw;
        }
      }
    }

    //------------------------------------------------------------------------
    //  [sftp.finfo.cache] キャッシュ版
    Gen::IEnumerable<FileInfo> GetList(System.DateTime referenceTime,string rpath){
      if(rpath.Length>1&&rpath[rpath.Length-1]=='/')
        rpath=rpath.Substring(0,rpath.Length-1);

      Gen::IEnumerable<FileInfo> ret;
      if(this.ficache.TryGetList(referenceTime,rpath,out ret))return ret;
      lock(ficache){ // (double-checked locking)
        if(this.ficache.TryGetList(referenceTime,rpath,out ret))return ret;
        return this.UpdateList(rpath);
      }

      //double-checked lock をするのは...
      //  或るディレクトリの情報を読み取っている間に、
      //  同じディレクトリに対する要求があった場合に、
      //  二回読み取りを実行するのを防ぐ為。
      //例:
      //  C L C -------------R U
      //              C -------L C U
      //  C: check; R: 読取; L: lock; U: unlock;
    }

    FileInfo GetFile(System.DateTime referenceTime,string path){
      bool mustbe_directory=false;
      if(path.Length>=2&&path[path.Length-1]=='/'){
        path=path.Substring(0,path.Length-1);
        mustbe_directory=true;
      }

      //■lock(stat_cache) は必要か?

      FileInfo info;
      if(!this.ficache.TryGetFile(referenceTime,path,out info)){
        // リスト更新ができる場合
        
        this.GetList(referenceTime,this.GetParentDirectoryPath(path));
        if(!this.ficache.TryGetFile(referenceTime,path,out info)){
          //return this.UpdateFile(path);
          session.Message.Write(1,"! cache: not found {0}",path);
          return null;
        }
      }

      if(info==null||mustbe_directory&&!info.IsDirectory)
        return null;
      return info;
    }

    //------------------------------------------------------------------------
    //  [sftp.finfo.public] FileInfo 公開用にリンクを解決
    class readlink_cache_entry{
      public System.DateTime informationTime;
      public string targetPath=null;
      public byte[] cyglink_data=null;
    }
    readonly Gen::Dictionary<string,readlink_cache_entry> readlink_cache
      =new Gen::Dictionary<string,readlink_cache_entry>();
    private readlink_cache_entry LinkGetEntry(FileInfo link){
      string rpath=link.InternalPath;

      readlink_cache_entry entry;
      if(readlink_cache.TryGetValue(rpath,out entry)){
        if(link.LastWriteTime<=entry.informationTime)
          return entry;
      }else{
        entry=new readlink_cache_entry();
        entry.informationTime=System.DateTime.MinValue;
        readlink_cache[rpath]=entry;
      }

      session.Message.Write(1,"$ readlink {0}",rpath);
      entry.targetPath=session.Sftp.noglob_readlink(rpath);
      entry.informationTime=System.DateTime.Now;
      entry.cyglink_data=null;
      return entry;
    }
    private string Readlink(FileInfo link){
      return LinkGetEntry(link).targetPath;
    }
    internal byte[] LinkGetCyglinkData(FileInfo link){
      readlink_cache_entry entry=LinkGetEntry(link);

      if(entry.cyglink_data==null){
        string content=entry.targetPath;
        if(content.Length>0&&content[0]=='/'&&content.StartsWith(this.RootDirectory)){
          content=Unix.UnixPath.GetRelativePathTo(content,Unix.UnixPath.GetDirectoryPath(link.InternalPath));
        }

        byte[] data=new byte[System.Text.Encoding.Unicode.GetByteCount(content)+14];
        System.Text.Encoding.ASCII.GetBytes("!<symlink>",0,10,data,0);
        data[10]=(byte)'\xFF';
        data[11]=(byte)'\xFE';
        System.Text.Encoding.Unicode.GetBytes(content,0,content.Length,data,12);
        data[data.Length-2]=(byte)'\0';
        data[data.Length-1]=(byte)'\0';
        entry.cyglink_data=data;
      }

      return entry.cyglink_data;
    }

    FileInfo Reparse(System.DateTime referenceTime,FileInfo info){
      return Reparse(referenceTime,info,10);
    }
    FileInfo Reparse(System.DateTime referenceTime,FileInfo info_,int hop){
      if(0==(info_.Attributes&System.IO.FileAttributes.ReparsePoint))return info_;
      SftpFileInfo info=(SftpFileInfo)info_;

      if(account.SymlinkTreatment==mwg.Sshfs.SftpSymlink.Dereference){
        string rpath=info.InternalPath;
        if(rpath.Length>0&&rpath[rpath.Length-1]=='/')
          rpath=rpath.Substring(0,rpath.Length-1);

        string rpath2=Readlink(info);
        //session.Message.Write(1,"$ readlink {0}",rpath);
        //string rpath2=session.Sftp.readlink(rpath);
        if(rpath2.Length==0)goto normal;
        rpath2=Unix.UnixPath.Combine(Unix.UnixPath.GetDirectoryPath(rpath),rpath2);

        SftpFileInfo info2=(SftpFileInfo)this.GetFile(referenceTime,rpath2);
        if(info2==null)goto normal;
        Reparse(referenceTime,info2,hop-1);

        info.Attributes    =info2.Attributes;
        info.Permission    =info2.Permission;
        info.Length        =info2.Length;
        info.LastWriteTime =info2.LastWriteTime;
        info.LastAccessTime=info2.LastAccessTime;
        info.CreationTime  =info2.CreationTime;
        info.JschAttribute =info2.JschAttribute;
        return info;
      normal:
        info.Attributes&=~System.IO.FileAttributes.ReparsePoint;
        return info;
      }
      //  ResolveLink(referenceTime,info,10);
      return info;
    }

    int InternalGetFileList(string rpath,out Gen::IEnumerable<FileInfo> list){
      System.DateTime now=System.DateTime.Now;
      list=this.GetList(now,rpath);
      if(list==null)return -WinErrorCode.ERROR_FILE_NOT_FOUND;

      foreach(FileInfo fi in list)
        this.Reparse(now,fi);
      return 0;
    }

    FileInfo InternalGetFileInfo(string rpath){
      System.DateTime now=System.DateTime.Now;
      FileInfo finfo=this.GetFile(now,rpath);
      if(finfo!=null)this.Reparse(now,finfo);
      return finfo;
    }
    #endregion

    //==========================================================================
    //  [sftp.change] ディレクトリ変更操作
    //------------------------------------------------------------------------
    int InternalSetFileInfo(string rpath,FileInfo finfo,SetFileInfoFlags flags){
      FileInfo fi=this.GetFileInfo(rpath);
      jsch::SftpATTRS attr=((SftpFileInfo)fi).JschAttribute;
      int flgXfer=0;

      if(0!=(flags&SetFileInfoFlags.SetPermission)){
        if(attr.getPermissions()!=finfo.Permission){
          flgXfer|=Tamir.SharpSsh.jsch.SftpATTRS.SSH_FILEXFER_ATTR_PERMISSIONS;

          session.Message.Write(1,"$ chmod {0} {1}",System.Convert.ToString(finfo.Permission,8),rpath);
          attr.setPERMISSIONS(fi.Permission);
        }
        if(finfo!=fi)
          fi.Permission=finfo.Permission;
      }

      if(0!=(flags&SetFileInfoFlags.SetFileSize)){
        if(attr.getSize()!=finfo.Length){
          flgXfer|=Tamir.SharpSsh.jsch.SftpATTRS.SSH_FILEXFER_ATTR_SIZE;
          
          session.Message.Write(1,"$ truncate -s {0} {1}",finfo.Length,rpath);
          attr.setSIZE(fi.Length);
        }
        if(finfo!=fi)
          fi.Length=finfo.Length;
      }

      /* 日時設定 */{
        int asecs;
        int msecs;
        if(0!=(flags&SetFileInfoFlags.SetFileMTime)){
          msecs=Unix.UnixTime.DateTimeToUnixTime(finfo.LastWriteTime);
          if(msecs!=attr.getMTime()){
            flgXfer|=Tamir.SharpSsh.jsch.SftpATTRS.SSH_FILEXFER_ATTR_ACMODTIME;

            session.Message.Write(1,"$ touch {0} -md {1}",rpath,finfo.LastWriteTime);
          }
          if(finfo!=fi)
            fi.LastWriteTime=finfo.LastWriteTime;

          // SFTP: ctime は mtime のミラー
          finfo.CreationTime=finfo.LastWriteTime;
          if(finfo!=fi)
            fi.CreationTime=finfo.LastWriteTime;
        }else{
          msecs=Unix.UnixTime.DateTimeToUnixTime(fi.LastWriteTime); // fi の方が最新情報なので。
        }

        if(0!=(flags&SetFileInfoFlags.SetFileATime)){
          asecs=Unix.UnixTime.DateTimeToUnixTime(finfo.LastAccessTime);
          if(asecs!=attr.getATime()){
            flgXfer|=Tamir.SharpSsh.jsch.SftpATTRS.SSH_FILEXFER_ATTR_ACMODTIME;

            session.Message.Write(1,"$ touch {0} -ad {1}",rpath,finfo.LastAccessTime);
          }
          if(finfo!=fi)
            fi.LastAccessTime=finfo.LastAccessTime;
        }else{
          asecs=Unix.UnixTime.DateTimeToUnixTime(fi.LastAccessTime); // fi の方が最新情報なので。
        }

        if((flgXfer&Tamir.SharpSsh.jsch.SftpATTRS.SSH_FILEXFER_ATTR_ACMODTIME)!=0)
          attr.setACMODTIME(asecs,msecs);
      }

      if(flgXfer!=0)
        session.Sftp.noglob_setstat(rpath,attr,flgXfer);

      return 0;
    }
    int InternalCreateDirectory(string rpath){
      session.Message.Write(1,"$ mkdir {0}",rpath);
      session.Sftp.noglob_mkdir(rpath);

      ficache.ClearFile(rpath);
      ficache.ClearList(Unix.UnixPath.GetDirectoryPath(rpath));
      return 0;
    }
    int InternalDeleteDirectory(string rpath){
      session.Message.Write(1,"$ rmdir {0}",rpath);
      session.Sftp.noglob_rmdir(rpath);

      ficache.ClearFile(rpath);
      ficache.ClearList(Unix.UnixPath.GetDirectoryPath(rpath));
      return 0;
    }
    int InternalCreateFile(string rpath){
      // 本当はファイルが存在していない事を想定
      FileInfo fi=this.GetFileInfo(rpath);
      if(fi!=null){
        this.InternalRemoveFile(rpath);
        //if(fi.Length!=0){
        //  fi.Length=0;
        //  session.Message.Write(1,"$ truncate -s 0 {0}",rpath);
        //  jsch::SftpATTRS attr=((SftpFileInfo)fi).JschAttribute;
        //  attr.setSIZE(0);
        //  session.Sftp.setStat(rpath,attr);
        //}
        //return 0;
      }

      session.Message.Write(1,"$ touch {0}",rpath);
      session.Sftp.noglob_put(rpath).Close();
      ficache.ClearFile(rpath);
      ficache.ClearList(Unix.UnixPath.GetDirectoryPath(rpath));
      return 0;
    }
    int InternalRemoveFile(string rpath){
      session.Message.Write(1,"$ rm {0}",rpath);
      session.Sftp.noglob_rm(rpath);
      ficache.ClearFile(rpath);
      ficache.ClearList(Unix.UnixPath.GetDirectoryPath(rpath));
      return 0;
    }
    int InternalMoveFile(string rpaths,string rpathd){
      try{
        session.Message.Write(1,"$ mv {0} {1}",rpaths,rpathd);
        session.Sftp.noglob_rename(rpaths,rpathd);
      }catch(jsch::SftpException e0){
        if(e0.message!="Failure")throw;
        string cmd=string.Format("mv {0} {1}",rpaths,rpathd);
        string hoge=session.Exec(cmd);
        session.Message.Write(5,"> result: "+hoge);
      }
      ficache.ClearFile(rpaths);
      ficache.ClearFile(rpathd);
      ficache.ClearList(Unix.UnixPath.GetDirectoryPath(rpaths));
      ficache.ClearList(Unix.UnixPath.GetDirectoryPath(rpathd));
      return 0;
    }

    //==========================================================================
    //  [sftp.readwrite] ファイル内容読み書き
    //--------------------------------------------------------------------------
    public void OpenFile(string rpath){ /* do nothing */ }
    public void CloseFile(string rpath){ /* do nothing */ }
    //--------------------------------------------------------------------------
    //  [sftp.readwrite.read]
    int InternalReadData(byte[] buff,int buffOffset,string path,long offset,ref int length){
      // check
      FileInfo fi=this.GetFileInfo(path);
      if(fi==null){
        length=0;
        return -WinErrorCode.ERROR_FILE_NOT_FOUND;
      }

      SftpFileInfo finfo=(SftpFileInfo)fi;
      if(finfo.FileType==SftpFileType.LinkAsCygwinSymlink){
        return ReadDataAsCygwinSymlink(finfo,buff,buffOffset,offset,ref length);
      }

      // 読取
      ReadProgress monitor=new ReadProgress(offset+length);
      ReadDataStream dst=new ReadDataStream(buff,buffOffset);
      session.Message.Write(1,"$ get {0} {1:X}-{2:X}",path,offset,offset+length);
      session.Sftp.noglob_get(path,dst,monitor,jsch::ChannelSftp.RESUME,offset);
      length=dst.ReceivedBytes;
      return 0;
    }

    int ReadDataAsCygwinSymlink(SftpFileInfo finfo,byte[] buff,int buffOffset,long offset,ref int length){
      // check
      if(offset<0)
        return -1;
      if(length<0){
        length=0;
        return -1;
      }

      byte[] data=this.LinkGetCyglinkData(finfo);
      if(length>buff.Length-buffOffset)
        length=buff.Length-buffOffset;
      if(length>data.Length-(int)offset)
        length=data.Length-(int)offset;
      if(length==0)return 0;

      System.Array.Copy(data,offset,buff,buffOffset,length);
      return 0;
    }

    class ReadDataStream:java::io.OutputStream{
      int offset;
      int index;
      byte[] buff;

      public ReadDataStream(byte[] buff):this(buff,0){}
      public ReadDataStream(byte[] buff,int offset){
        this.buff=buff;
        this.offset=offset;
        this.index=offset;
      }

      public override void Write(byte[] buffer,int offset,int count) {
        int rest=this.buff.Length-this.index;
        if(count>rest)count=rest;
        if(count<=0)return;
        System.Array.Copy(buffer,offset,this.buff,this.index,count);
        this.index+=count;
      }

      public int ReceivedBytes{
        get{return this.index-this.offset;}
      }
    }
    class ReadProgress:jsch::SftpProgressMonitor{
        private long length;
        private long index;
        public ReadProgress(long max){
            this.length=max;
        }
        public override bool count(long count){
            this.index+=count;
            return this.index<this.length;
        }
        public override void end(){}
        public override void init(int op, string src, string dest, long max){}
    }
    //--------------------------------------------------------------------------
    //  [sftp.readwrite.write]
    int InternalWriteData(byte[] buff,int buffOffset,string path,long offset,ref int length){
      // check
      SftpFileInfo finfo=(SftpFileInfo)this.GetFileInfo(path);
      if(finfo.IsReadOnly)
        return -WinErrorCode.ERROR_FILE_READ_ONLY;

      // 書込
      session.Message.Write(1,"$ put {0} {1:X}-{2:X}",path,offset,offset+length);
      java::io.OutputStream stream=session.Sftp.noglob_put(path,null,3,offset);
      int len=length;
      const int BLK=0x1000;
      while(len>BLK){
        stream.Write(buff,buffOffset,BLK);
        len-=BLK;
        buffOffset+=BLK;
      }
      stream.Write(buff,buffOffset,len);
      stream.Close();
      /* 一度に巨大なデータを書き込もうとするとエラーになる。
      java::io.OutputStream stream=session.Sftp.put(path,null,3,offset);
      stream.Write(buff,buffOffset,length);
      stream.Close();
      //*/

      // ファイル情報更新
      {
        finfo=(SftpFileInfo)this.GetFileInfo(path);
        SetFileInfoFlags sfi=SetFileInfoFlags.SetFileMTime;
        long size=finfo.Length;
        if(size<offset+length){
          //sfi|=SetFileInfoFlags.SetFileSize; // リモートには即座に反映しなくても OK
          finfo.Length=offset+length;
        }

        finfo.LastWriteTime=System.DateTime.Now;
        this.SetFileInfo(path,finfo,sfi);
      }
      //ficache.ApplyFile_WriteData(path,offset+length);
      return 0;
    }
    //==========================================================================
    //  [sftp.errorhandle] Error Handling and Interfaces
    //--------------------------------------------------------------------------
    public bool DetermineErrorCode(System.Exception e,out int code){
      code=-1;
      jsch::SftpException e1=e as jsch::SftpException;
      if(e1!=null){
        this.session.Message.Write(1,"! sftp: {0}",e1.message);
        switch((mwg.Unix.SSH_ERROR)e1.id){
          case Unix.SSH_ERROR.CONNECTION_CLOSED:
          case Unix.SSH_ERROR.CONNECTION_LOST:
          case Unix.SSH_ERROR.NO_CONNECTION:
            code=FsBasicUtil.ErrorRequestReconnection;
            break;
          case Unix.SSH_ERROR.PERMISSION_DENIED:
            code=-WinErrorCode.ERROR_ACCESS_DENIED;
            break;
          case Unix.SSH_ERROR.WRITE_PROTECT:
            code=-WinErrorCode.ERROR_WRITE_PROTECT;
            break;
          case Unix.SSH_ERROR.FILE_ALREADY_EXISTS:
            code=-WinErrorCode.ERROR_ALREADY_EXISTS;
            break;
          case Unix.SSH_ERROR.NO_SUCH_PATH:
            code=-WinErrorCode.ERROR_PATH_NOT_FOUND;
            break;
          case Unix.SSH_ERROR.NO_SUCH_FILE:
            code=-WinErrorCode.ERROR_FILE_NOT_FOUND;
            break;
          case mwg.Unix.SSH_ERROR.NO_SPACE_ON_FILESYSTEM:
          case mwg.Unix.SSH_ERROR.QUOTA_EXCEEDED:
            code=-WinErrorCode.ERROR_DISK_FULL;
            break;
          case mwg.Unix.SSH_ERROR.INVALID_FILENAME:
            code=-WinErrorCode.ERROR_INVALID_NAME;
            break;
          default:
            code=-1;
            break;
        }
        return true;
      }

      return false;
    }
    public int GetFileList(string rpath,out Gen::IEnumerable<FileInfo> list){
      list=null;
      try{
        return this.InternalGetFileList(rpath,out list);
      }catch(System.Exception e){
        int code;
        if(this.DetermineErrorCode(e,out code))
          return code;
        else throw;
      }
    }
    public FileInfo GetFileInfo(string rpath){
      try{
        return this.InternalGetFileInfo(rpath);
      }catch(System.Exception e){
        int code;
        if(this.DetermineErrorCode(e,out code))
          throw new FsBasicReturnException(code);
        else throw;
      }
    }
    public int SetFileInfo(string rpath,FileInfo finfo,SetFileInfoFlags flags){
      System.Diagnostics.Debug.Assert(!this.IsReadOnly,"invalid program: readonly");
      try{
        return this.InternalSetFileInfo(rpath,finfo,flags);
      }catch(System.Exception e){
        int code;
        if(this.DetermineErrorCode(e,out code))
          return code;
        else throw;
      }
    }
    public int CreateDirectory(string rpath){
      System.Diagnostics.Debug.Assert(!this.IsReadOnly,"invalid program: readonly");
      try{
        return this.InternalCreateDirectory(rpath);
      }catch(System.Exception e){
        int code;
        if(this.DetermineErrorCode(e,out code))
          return code;
        else throw;
      }
    }
    public int RemoveDirectory(string rpath){
      System.Diagnostics.Debug.Assert(!this.IsReadOnly,"invalid program: readonly");
      try{
        return this.InternalDeleteDirectory(rpath);
      }catch(System.Exception e){
        int code;
        if(this.DetermineErrorCode(e,out code))
          return code;
        else throw;
      }
    }
    public int CreateFile(string rpath){
      System.Diagnostics.Debug.Assert(!this.IsReadOnly,"invalid program: readonly");
      try{
        return this.InternalCreateFile(rpath);
      }catch(System.Exception e){
        int code;
        if(this.DetermineErrorCode(e,out code))
          return code;
        else throw;
      }
    }
    public int RemoveFile(string rpath){
      System.Diagnostics.Debug.Assert(!this.IsReadOnly,"invalid program: readonly");
      try{
        return this.InternalRemoveFile(rpath);
      }catch(System.Exception e){
        int code;
        if(this.DetermineErrorCode(e,out code))
          return code;
        else throw;
      }
    }
    public int MoveFile(string rpaths,string rpathd){
      System.Diagnostics.Debug.Assert(!this.IsReadOnly,"invalid program: readonly");
      try{
        return this.InternalMoveFile(rpaths,rpathd);
      }catch(System.Exception e){
        int code;
        if(this.DetermineErrorCode(e,out code))
          return code;
        else throw;
      }
    }
    public int ReadData(byte[] buff,int buffOffset,string path,long offset,ref int length){
      try{
        return this.InternalReadData(buff,buffOffset,path,offset,ref length);
      }catch(System.Exception e){
        int code;
        if(this.DetermineErrorCode(e,out code))
          return code;
        else throw;
      }
    }
    public int WriteData(byte[] buff,int buffOffset,string path,long offset,ref int length){
      System.Diagnostics.Debug.Assert(!this.IsReadOnly,"invalid program: readonly");
      try{
        return this.InternalWriteData(buff,buffOffset,path,offset,ref length);
      }catch(System.Exception e){
        int code;
        if(this.DetermineErrorCode(e,out code))
          return code;
        else throw;
      }
    }
    //--------------------------------------------------------------------------
  }

}

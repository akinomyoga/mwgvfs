using Gen=System.Collections.Generic;

namespace mwg.mwgvfs.gvfs{
  //[System.Serializable]
  //[System.Flags]
  //[System.Runtime.InteropServices.ComVisible(true)]
  //public enum FileFlags{
  //  ReadOnly                =0x0001,
  //  Hidden                  =0x0002,
  //  System                  =0x0004,
  //  Directory               =0x0010,
  //  Archive                 =0x0020,
  //  Device                  =0x0040,
  //  Normal                  =0x0080,
  //  Temporary               =0x0100,
  //  SparseFile              =0x0200,
  //  ReparsePoint            =0x0400,
  //  Compressed              =0x0800,
  //  Offline                 =0x1000,
  //  NotContentIndexed       =0x2000,
  //  Encrypted               =0x4000,
  //}

  public class FileInfo{
    //readonly Dokan.DokanOperations operation;
    readonly string filepath;
    readonly string name;

    internal System.DateTime informationTime;
    long filesize;
    int permission=0x1FF;
    System.IO.FileAttributes flags;
    System.DateTime accessedTime;
    System.DateTime modifiedTime;
    System.DateTime creationTime;

    public FileInfo(string filepath,string name,System.DateTime informationTime){
      this.informationTime=informationTime;
      //this.operation=operation;
      this.filepath=filepath;
      this.name=name;
    }
    public FileInfo(string filepath,string name):this(filepath,name,System.DateTime.Now){}
    public FileInfo(string filepath,string name,FileInfo copye)
      :this(filepath,name,copye.informationTime)
    {
      this.filesize=copye.Length;
      this.permission=copye.Permission;
      this.flags=copye.Attributes;
      this.accessedTime=copye.LastAccessTime;
      this.modifiedTime=copye.LastWriteTime;
      this.creationTime=copye.CreationTime;
    }
    public FileInfo(FileInfo copye):this(copye.filepath,copye.name,copye){}

    /// <summary>
    /// 内部で使用しているファイルパスを取得します。
    /// </summary>
    public string InternalPath{
      get{return this.filepath;}
    }
    /// <summary>
    /// 内部で使用しているファイル名を取得します。
    /// </summary>
    public string InternalName{
      get{return Unix.UnixPath.GetFileName(filepath);}
    }
    /// <summary>
    /// Windows から参照する時のファイル名を取得します。
    /// </summary>
    public string Name{
      get{return this.name;}
    }
    public System.IO.FileAttributes Attributes{
      get{return this.flags;}
      set{this.flags=value;}
    }
    public int Permission{
      get{return this.permission;}
      set{this.permission=value;}
    }

    public System.DateTime LastAccessTime{
      get{return this.accessedTime;}
      set{this.accessedTime=value;}
    }
    public System.DateTime LastWriteTime{
      get{return this.modifiedTime;}
      set{this.modifiedTime=value;}
    }
    public System.DateTime CreationTime{
      get{return this.creationTime;}
      set{this.creationTime=value;}
    }

    public bool IsDirectory{
      get{return (this.flags&System.IO.FileAttributes.Directory)!=0;}
    }
    public bool IsReadOnly{
      get{return (this.flags&System.IO.FileAttributes.ReadOnly)!=0;}
    }
    public virtual long Length{
      get{return this.filesize;}
      set{this.filesize=value;}
    }

    //========================================================================
    //  CreateInstances
    //------------------------------------------------------------------------
    internal bool IsNotExist{
      get{return this.name==null;}
    }
    internal static FileInfo CreateNotExist(string filepath,System.DateTime informationTime){
      return new FileInfo(filepath,null,informationTime);
    }
    const int PermissionUmask      =(0<<6|2<<3|2);
    /// <summary>
    /// ファイルの既定の permission です。
    /// </summary>
    const int PermissionOfFile     =(6<<6|6<<3|6)&~PermissionUmask;
    /// <summary>
    /// ディレクトリの既定の permission です。
    /// </summary>
    const int PermissionOfDirectory=(7<<6|7<<3|7)&~PermissionUmask;
    /// <summary>
    /// 新規ファイルに対するファイル情報を生成します。
    /// </summary>
    /// <param name="path">ファイルの内部名を指定します。</param>
    /// <param name="name">ファイル名を指定します。</param>
    /// <param name="creationTime">ファイルが新規作成された日時を指定します。</param>
    /// <returns>ファイル情報を返します。</returns>
    public static FileInfo CreateNewFile(string path,string name,System.DateTime creationTime){
      FileInfo fi=new FileInfo(path,name,System.DateTime.Now);
      fi.CreationTime=creationTime;
      fi.LastAccessTime=creationTime;
      fi.LastWriteTime=creationTime;
      fi.Length=0;
      fi.Permission=PermissionOfFile;
      return fi;
    }
    /// <summary>
    /// 新規ディレクトリに対するファイル情報を生成します。
    /// </summary>
    /// <param name="path">ディレクトリの内部名を指定します。</param>
    /// <param name="name">ディレクトリ名を指定します。</param>
    /// <param name="creationTime">ディレクトリが新規作成された日時を指定します。</param>
    /// <returns>ファイル情報を返します。</returns>
    public static FileInfo CreateNewDirectory(string path,string name,System.DateTime creationTime){
      FileInfo fi=new FileInfo(path,name,System.DateTime.Now);
      fi.CreationTime=creationTime;
      fi.LastAccessTime=creationTime;
      fi.LastWriteTime=creationTime;
      fi.Length=0;
      fi.Permission=PermissionOfDirectory;
      fi.Attributes=System.IO.FileAttributes.Directory;
      return fi;
    }
  }

  class FileInfoCache{
    static System.TimeSpan TIMEOUT=new System.TimeSpan(0,0,5);

    struct DirEntries{
      public readonly Gen::List<FileInfo> list;
      public readonly System.DateTime informationTime;
      public DirEntries(Gen::List<FileInfo> result,System.DateTime date){
        this.list=result;
        this.informationTime=date;
      }
    }

    object sync_root=new object();
    Gen::Dictionary<string,FileInfo> dicattr=new Gen::Dictionary<string,FileInfo>();
    Gen::Dictionary<string,DirEntries> diclist=new Gen::Dictionary<string,DirEntries>();

    public FileInfoCache(){}

    public bool TryGetFile(string path,out FileInfo info){
      return this.TryGetFile(System.DateTime.Now,path,out info);
    }
    public bool TryGetList(string path,out Gen::IEnumerable<FileInfo> list){
      return this.TryGetList(System.DateTime.Now,path,out list);
    }
    /// <summary>
    /// 指定したファイルについてのファイル情報を取得します。
    /// </summary>
    /// <param name="referenceTime">情報の期限と比較する参照時刻を指定します。</param>
    /// <param name="path">ファイルの内部名を指定します。</param>
    /// <param name="info">
    /// キャッシュされたファイル情報が見付かった時、
    /// ファイルが存在していればファイル情報を返します。
    /// ファイルが存在していないという情報があった null を返します。
    /// ファイル情報が見付からなかった時は null を返します。
    /// </param>
    /// <returns>
    /// キャッシュされたファイル情報が見付かった時に true を返します。
    /// true が返った場合でも、ファイルが存在しないという情報が見付かった時には info は null になります。
    /// それ以外の場合に false を返します。
    /// </returns>
    public bool TryGetFile(System.DateTime referenceTime,string path,out FileInfo info){
      this.DiscardOld(referenceTime);

      bool ret=dicattr.TryGetValue(path,out info);
      if(ret&&info.IsNotExist)info=null;
      return ret;
    }
    public bool TryGetList(System.DateTime referenceTime,string path,out Gen::IEnumerable<FileInfo> list){
      this.DiscardOld(referenceTime);

      DirEntries value;
      if(diclist.TryGetValue(path,out value)){
        list=value.list;
        return true;
      }else{
        list=null;
        return false;
      }
    }

    public bool ClearFile(string path){
      lock(sync_root)return dicattr.Remove(path);
    }
    public bool ClearList(string path){
      lock(sync_root)return diclist.Remove(path);
    }
    static void ApplyFileInfo_WriteData(FileInfo fi,System.DateTime now,long minFilesize){
      if(fi.Length<minFilesize)fi.Length=minFilesize;
      fi.LastWriteTime=now;
    }
    public bool ApplyFile_WriteData(string path,string lname,long minFilesize){
      System.DateTime now=System.DateTime.Now;
      FileInfo fi;
      if(this.TryGetFile(now,path,out fi)&&fi!=null){
        ApplyFileInfo_WriteData(fi,now,minFilesize);
      }else{
        fi=FileInfo.CreateNewFile(path,lname,now);
        fi.Length=minFilesize;
        this.SetFile(fi);
      }

      return true;
    }
    public bool ApplyList_WriteData(string dirpath,string lname,long minFilesize){
      System.DateTime now=System.DateTime.Now;
      Gen::IEnumerable<FileInfo> list;
      if(this.TryGetList(now,dirpath,out list)){
        if(list!=null)
          foreach(FileInfo fi in list){
            if(fi.Name==lname){
              ApplyFileInfo_WriteData(fi,now,minFilesize);
              return true;
            }
          }
      }
      return false;
    }
    static void ApplyFileInfo_SetFileInfo(FileInfo fi,FileInfo finfo,SetFileInfoFlags flags){
      if(fi==finfo)return;

      if((flags&SetFileInfoFlags.SetFileSize)!=0){
        fi.Length=finfo.Length;
      }

      if((flags&SetFileInfoFlags.SetFileMTime)!=0)
        if(finfo.LastWriteTime!=System.DateTime.MinValue)
          fi.LastWriteTime=finfo.LastWriteTime;
      if((flags&SetFileInfoFlags.SetFileATime)!=0)
        if(finfo.LastAccessTime!=System.DateTime.MinValue)
          fi.LastAccessTime=finfo.LastAccessTime;
      if((flags&SetFileInfoFlags.SetFileCTime)!=0)
        if(finfo.CreationTime!=System.DateTime.MinValue)
          fi.CreationTime=finfo.CreationTime;

      if((flags&SetFileInfoFlags.SetPermission)!=0){
        fi.Permission=finfo.Permission;
      }
    }
    public bool ApplyFile_SetFileInfo(string path,FileInfo finfo,SetFileInfoFlags flags){
      System.DateTime now=System.DateTime.Now;
      FileInfo fi;
      if(this.TryGetFile(now,path,out fi)&&fi!=null){
        ApplyFileInfo_SetFileInfo(fi,finfo,flags);
        return true;
      }else{
        return false;
      }
    }
    public bool ApplyList_SetFileInfo(string dirname,string name,FileInfo finfo,SetFileInfoFlags flags){
      System.DateTime now=System.DateTime.Now;
      Gen::IEnumerable<FileInfo> list;
      if(this.TryGetList(now,dirname,out list)){
        if(list!=null)
          foreach(FileInfo fi in list){
            if(fi.Name==name){
              ApplyFileInfo_SetFileInfo(fi,finfo,flags);
              return true;
            }
          }
      }
      return false;
    }

    public void SetFileNotFound(string path){
      this.SetFile(FileInfo.CreateNotExist(path,System.DateTime.Now));
    }
    public void SetFile(FileInfo info){
      lock(this.sync_root){
        dicattr[info.InternalPath]=info;
        if(info.informationTime<this.oldestInfoTime)
          this.oldestInfoTime=info.informationTime;
      }
    }
    public void SetList(string path,Gen::IEnumerable<FileInfo> entries){
      string dir=path;
      if(!dir.EndsWith("/"))dir+="/";

      System.DateTime now=System.DateTime.Now;
      lock(this.sync_root){
        Gen::List<FileInfo> list=null;
        if(entries!=null){
          list=new Gen::List<FileInfo>();
          foreach(FileInfo info in entries){
            dicattr[info.InternalPath]=info;
            if(info.InternalPath!=path)
              list.Add(info);
            if(info.informationTime<this.oldestInfoTime)
              this.oldestInfoTime=info.informationTime;
          }
        }
        diclist[path]=new DirEntries(list,now);
        if(now<this.oldestInfoTime)
          this.oldestInfoTime=now;
      }

      this.DiscardOld();
    }

    // 古い情報を削除
    System.DateTime oldestInfoTime=System.DateTime.MaxValue;
    void DiscardOld(){
      this.DiscardOld(System.DateTime.Now);
    }
    void DiscardOld(System.DateTime referenceTime){
      System.DateTime dthresh=referenceTime-TIMEOUT;
      if(this.oldestInfoTime>dthresh)return;

      lock(sync_root){
        System.DateTime oldest=System.DateTime.MaxValue;

        Gen::List<string> remove_key=new Gen::List<string>();
        foreach(Gen::KeyValuePair<string,FileInfo> pair in this.dicattr){
          System.DateTime infotime=pair.Value.informationTime;
          if(infotime<dthresh)
            remove_key.Add(pair.Key);
          else if(infotime<oldest)
            oldest=infotime;
        }
        foreach(string k in remove_key)this.dicattr.Remove(k);

        remove_key.Clear();
        foreach(Gen::KeyValuePair<string,DirEntries> pair in this.diclist){
          System.DateTime infotime=pair.Value.informationTime;
          if(infotime<dthresh)
            remove_key.Add(pair.Key);
          else if(infotime<oldest)
            oldest=infotime;
        }
        foreach(string k in remove_key)this.diclist.Remove(k);

        this.oldestInfoTime=oldest;
      }
    }
  }

}

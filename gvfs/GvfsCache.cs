//#define READ_CACHE_L1

using Gen=System.Collections.Generic;

using AfhPath=afh.Application.Path;

namespace mwg.mwgvfs.gvfs {

  #region ReadData Cache
  class CacheBlock{
    internal long position;
    internal System.DateTime updatedTime;

    public CacheBlock(int iblk){
      this.position=iblk*(long)CacheBlockStore.BlockSize;
      this.updatedTime=System.DateTime.MinValue;
    }

    public System.DateTime UpdateTime{
      get{return this.updatedTime;}
    }
  }

  class CacheBlockStore:System.IDisposable{
    public const int BlockSize=0x1000;
    public const int BlockMask=0x0FFF;

    public CacheBlockStore(){
      this.InitializeCacheFile();
    }

    //------------------------------------------------------------------------
    //  一時ファイルの管理
    internal static readonly System.Random rand=new System.Random();
    System.IO.FileStream stream;
    string temporaryFilePath;
    void InitializeCacheFile(){
      // cachedir
      string cachedir=AfhPath.Combine(AfhPath.ExecutableDirectory,"cache");
      AfhPath.EnsureDirectoryExistence(ref cachedir);

      // temppath
      temporaryFilePath=AfhPath.Combine(cachedir,rand.Next(0x1000000).ToString("X6"));
      temporaryFilePath=AfhPath.GetAvailablePath(temporaryFilePath,"tmp");
      this.stream=System.IO.File.Open(
        temporaryFilePath,
        System.IO.FileMode.CreateNew,
        System.IO.FileAccess.ReadWrite
        );
    }

    public void Dispose(){
      lock(files){
        foreach(CacheFile file in files.Values)
          file.Dispose();
        files.Clear();
      }

      if(this.stream!=null){
        lock(this.stream){
          this.stream.Close();
          this.stream=null;
          System.IO.File.Delete(this.temporaryFilePath);
          this.temporaryFilePath=null;
        }
      }
    }

    //------------------------------------------------------------------------
    //  一時ファイル上のブロックの管理
    int nblocks=0;
    Gen::List<CacheBlock> freeblocks=new Gen::List<CacheBlock>();

    public CacheBlock AllocBlock(){
      lock(freeblocks){
        if(freeblocks.Count==0)
          return new CacheBlock(nblocks++);
        CacheBlock ret=freeblocks[freeblocks.Count-1];
        freeblocks.RemoveAt(freeblocks.Count-1);
        return ret;
      }
    }
    public void FreeBlock(CacheBlock block){
      lock(freeblocks){
        freeblocks.Add(block);
      }
    }
    public void ReadBlock(CacheBlock block,byte[] buff,int buffOffset,int offset,int length){
      lock(this.stream){
        this.stream.Seek(block.position+offset,System.IO.SeekOrigin.Begin);
        length=this.stream.Read(buff,buffOffset,length);
      }
    }
    public void WriteBlock(CacheBlock block,byte[] buff,int buffOffset,int offset,int length){
      lock(this.stream){
        this.stream.Seek(block.position+offset,System.IO.SeekOrigin.Begin);
        this.stream.Write(buff,buffOffset,length);
      }
      block.updatedTime=System.DateTime.Now;
    }

    //------------------------------------------------------------------------
    Gen::Dictionary<string,CacheFile> files=new Gen::Dictionary<string,CacheFile>();
  }

  class CacheFile :System.IDisposable{
    public string name;
    public IFsBasic basic;
    public CacheBlockStore store;
    public Gen::Dictionary<int,CacheBlock> map=new Gen::Dictionary<int,CacheBlock>();
    public System.DateTime mtime=System.DateTime.MinValue;
    public long filesize;

    public CacheFile(string name,IFsBasic basic,CacheBlockStore store){
      this.name=name;
      this.basic=basic;
      this.store=store;
    }
    public void Dispose(){
      this.Clear();
    }
    public void Clear(){
      lock(map){
        foreach(CacheBlock b in this.map.Values)
          store.FreeBlock(b);
        map.Clear();
      }
    }
    public void Update(){
      bool dirty=false;
      lock(map){
        FileInfo fi=basic.GetFileInfo(name);
        if(fi==null){
          this.Clear();
          return;
        }

        long filesize=fi.Length;
        if(this.filesize!=filesize){
          dirty=true;
          this.filesize=filesize;
        }

        System.DateTime mtime=fi.LastWriteTime;
        if(this.mtime!=mtime){
          dirty=true;
          this.mtime=mtime;
        }

        if(dirty)this.Clear();
      }
    }
    //FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF
    //  Read
    //========================================================================
    public int Read(byte[] buff,int buffOffset,long offset,ref int rlength){
      // ファイルサイズ制限
      if(rlength>filesize-offset)
        rlength=(int)(filesize-offset);
      if(rlength<=0){
        rlength=0;
        return 0;
      }

      ReadParams p=new ReadParams(this,buff,buffOffset,offset,rlength);
      lock(map){
        if(p.ReadHeadFromCache()){
          rlength=p.ReadLength;
          return 0;
        }

        p.DetectSequentialAccess();
        p.ReadTailFromCache();

        // データ取得
        int r=p.ExecuteDownload();if(r!=0)return r;
        rlength=p.ReadLength;
        return 0;
      }
    }
    struct ReadParams{
      CacheFile file;
      byte[] buff;

      int  data_off_buff;
      long data_off_file;
      int  data_len     ;

      int boff;
      int bend;

      int read_len;
      int read_len_tail;

      public ReadParams(CacheFile file,byte[] buff,int buffOffset,long offset,int rlength){
        this.file=file;
        this.buff=buff;

        this.data_off_buff = buffOffset;
        this.data_off_file = offset;
        this.data_len      = rlength;
        this.boff=(int)(data_off_file/CacheBlockStore.BlockSize);
        this.bend=required_blocks(data_off_file+rlength);

        this.read_len=0;
        this.read_len_tail=0;
      }
      //----------------------------------------------------------------------
      /// <summary>
      /// キャッシュを保持している部分をダウンロード範囲から省略します。
      /// 要求されているデータの先頭部分がキャッシュされているかどうかを調べ
      /// キャッシュされていた場合にはその部分はキャッシュから読み取り、
      /// ダウンロード範囲を残りの部分に縮小します。
      /// </summary>
      /// <returns>全てのデータがキャッシュにあり読取が完了した場合に true を返します。</returns>
      public bool ReadHeadFromCache(){
        // 既にデータを持っている部分は処理
        CacheBlock b;
        while(boff<bend&&file.map.TryGetValue(boff,out b)){
          int copy_off_block=(int)(data_off_file-boff*(long)CacheBlockStore.BlockSize);
          int copy_len=CacheBlockStore.BlockSize-copy_off_block;
          if(copy_len>data_len)copy_len=data_len;
          file.store.ReadBlock(b,buff,data_off_buff,copy_off_block,copy_len);

          // 更新
          data_off_buff+=copy_len;
          data_off_file+=copy_len;
          data_len-=copy_len;
          read_len+=copy_len;
          boff++;
        }

        return boff==bend;
      }
      /// <summary>
      /// キャッシュを保持している部分をダウンロード範囲から省略します。
      /// 要求されているデータの末端部分がキャッシュされているかどうかを調べ、
      /// キャッシュされていた場合にはその部分はキャッシュから読み取り、
      /// ダウンロード範囲を残りの部分に縮小します。
      /// </summary>
      public void ReadTailFromCache(){
        // 末端からも処理
        CacheBlock b;
        while(boff<bend-1&&file.map.TryGetValue(bend-1,out b)){
          int copy_off_data=(int)((bend-1)*(long)CacheBlockStore.BlockSize-data_off_file);
          int copy_off_buff=data_off_buff+copy_off_data;
          int copy_len=(int)(data_len-copy_off_data);
          if(copy_len>0){
            file.store.ReadBlock(b,buff,copy_off_buff,0,copy_len);

            data_len-=copy_len;
            read_len_tail+=copy_len;
          }
          bend--;
        }
      }
      /// <summary>
      /// データのダウンロード範囲の拡張を実行します。
      /// データを連続的に順番にアクセスしている事が検出できた場合、
      /// 将来アクセスされると期待される先の方のデータも纏めてダウンロードする事にします。
      /// </summary>
      public void DetectSequentialAccess(){
        const int MIN_BLK=2;   // まとめ読み最小ブロック数
        const int MAX_BLK=16;  // まとめ読み最大ブロック数
        const int FACTOR =2;   // 最近読んだ長さの 1/FACTOR を先読みするか
        const int TIMESPAN=5;  // 「最近」とは何秒前か

        if(data_len>=MAX_BLK*CacheBlockStore.BlockSize&&boff<FACTOR*MIN_BLK)return;


        int nblk; // 少なくともまとめて先読みしたい量
        {
          System.DateTime time_thresh=System.DateTime.Now.AddSeconds(-TIMESPAN);

          int i=boff;
          int iM=boff-MAX_BLK*FACTOR;
          if(iM<0)iM=0;
          while(i>=iM){
            CacheBlock b;
            if(file.map.TryGetValue(i,out b)&&b.UpdateTime<time_thresh)break;
            i--;
          }
          nblk=(boff-i)/FACTOR;
          if(nblk<MIN_BLK)return;
        }

        // 元からそれ位は読もうと思っていた?
        if(bend>=boff+nblk)return;
        bend=boff+nblk;

        // filesize 制限
        int bfile=required_blocks(file.filesize);
        if(bend>bfile)bend=bfile;
      }
      public int ExecuteDownload(){
        long load_off_file=boff*(long)CacheBlockStore.BlockSize;
        long load_end_file=bend*(long)CacheBlockStore.BlockSize;
        if(load_end_file>file.filesize)
          load_end_file=file.filesize;
        int load_len=(int)(load_end_file-load_off_file);
        byte[] nbuff=new byte[CacheBlockStore.BlockSize*required_blocks(load_len)];

        int returnCode=file.basic.ReadData(nbuff,0,file.name,load_off_file,ref load_len);
        //if(returnCode!=0)return returnCode;
        file.AddDataToCache(nbuff,boff,load_len);

        // 結果書込
        int data_off_nbuff=(int)(data_off_file-load_off_file);
        if(load_len-data_off_nbuff<data_len){
          data_len=load_len-data_off_nbuff;
        }else{
          read_len+=read_len_tail;
        }
        read_len+=data_len;
        System.Array.Copy(nbuff,data_off_nbuff,buff,data_off_buff,data_len);
        return returnCode;
      }
      //----------------------------------------------------------------------
      public int ReadLength{
        get{return read_len;}
      }
      static int required_blocks(long size){
        return (int)((size+CacheBlockStore.BlockMask)/CacheBlockStore.BlockSize);
      }
    }
    /// <summary>
    /// 新しくダウンロードされたデータをキャッシュに追加します。
    /// </summary>
    /// <param name="buffer">新しいデータを格納している配列を指定します。</param>
    /// <param name="boff">新しいデータの開始ブロック番号を指定します。</param>
    /// <param name="len">新しいデータのデータサイズを指定します。</param>
    void AddDataToCache(byte[] buffer,int boff,int len){
      long off=boff*CacheBlockStore.BlockSize;
      long end=off+len;

      // どのブロックまで取得成功しているか
      int iM;
      if(end==filesize){
        iM=(int)((end+(CacheBlockStore.BlockSize-1))/CacheBlockStore.BlockSize);
      }else{
        iM=(int)(end/CacheBlockStore.BlockSize);
      }

      for(int i=boff;i<iM;i++){
        CacheBlock b;
        if(!map.TryGetValue(i,out b)){
          b=store.AllocBlock();
          map.Add(i,b);
        }
        store.WriteBlock(b,buffer,(i-boff)*CacheBlockStore.BlockSize,0,CacheBlockStore.BlockSize);
      }
    }
  }
  #endregion

  #region CacheDestructiveOperation
  /// <summary>
  /// キャッシュされた変更操作の種類を指定します。
  /// </summary>
  enum CacheChangeType{
    CreateDirectory,
    RemoveDirectory,
    CreateFile,
    MoveFile,
    RemoveFile,
    WriteData,
    SetFileInfo,
  }
  /// <summary>
  /// 指定パスにあるファイル(・ディレクトリ)に対する操作を表します。
  /// </summary>
  enum CacheChangeTraceStep{
    Trivial=0,
    /// <summary>
    /// この操作の直後にファイルが存在しない事を示します。
    /// </summary>
    PathRemoved,
    /// <summary>
    /// ファイルがこの操作によって新しく生成される事を示します。
    /// 既存のファイルの情報はこの操作によって全てなくなります。
    /// つまり、この操作よりも前に操作を遡る必要がない事を示唆します。
    /// その他のファイルには変更は及びません。
    /// </summary>
    PathCreateNew,
    /// <summary>
    /// 仮にファイルが存在していなくても、
    /// この操作によってファイルが生成される事を示します。
    /// その他のファイルには変更は及びません (親ディレクトリの一覧表示は除く)。
    /// </summary>
    PathCreate,
    /// <summary>
    /// ファイルに対して変更が適用されます。
    /// その他のファイルには変更は及びません (親ディレクトリの一覧表示は除く)。
    /// </summary>
    PathChange,
    /// <summary>
    /// この操作によってディレクトリ内容に変更が生じます。
    /// </summary>
    ListChange,
  }
  /// <summary>
  /// キャッシュされた変更操作の情報を保持します。
  /// </summary>
  abstract class CacheChange{
    readonly CacheChangeType opcode;
    readonly string fsid;
    readonly string rpath;
    readonly System.DateTime operationTime;

    /// <summary>
    /// 操作の種類を取得します。
    /// </summary>
    public CacheChangeType Type{
      get{return opcode;}
    }
    /// <summary>
    /// 操作対象のファイル名を取得します。
    /// </summary>
    public string Path{
      get{return rpath;}
    }
    /// <summary>
    /// 捜査対象のファイルが存在しているディレクトリのパスを取得します。
    /// </summary>
    public string ParentDirectory{
      get{return CachedFsBasic.GetParentDirectory(this.Path);}
    }
    /// <summary>
    /// 操作が実行された時刻を取得します。
    /// </summary>
    public System.DateTime OperationTime{
      get{return this.operationTime;}
    }

    protected CacheChange(string fsid,string rpath,CacheChangeType opcode){
      this.operationTime=System.DateTime.Now;
      this.opcode=opcode;
      this.fsid=fsid;
      this.rpath=rpath;

      System.Console.WriteLine("** WCache: {0} {1}",opcode,rpath);
    }

    //========================================================================
    //  キャッシュファイル
    //------------------------------------------------------------------------
    string tmpfilepath;
    const uint TMPFILE_MAGIC=0xCacbeDa7;
    static string WCacheTmpname(string fsid){
      string cachedir=AfhPath.ExecutableDirectory;
      cachedir=AfhPath.Combine(cachedir,"cache");
      cachedir=AfhPath.Combine(cachedir,fsid);
      AfhPath.EnsureDirectoryExistence(ref cachedir);

      // temppath
      string tmpfilepath=System.DateTime.Now.Ticks.ToString("X16")
        +"-"+CacheBlockStore.rand.Next(int.MaxValue).ToString("X8");
      tmpfilepath=AfhPath.Combine(cachedir,tmpfilepath);
      tmpfilepath=AfhPath.GetAvailablePath(tmpfilepath,"write");
      return tmpfilepath;
    }
    static void WCacheWriteHeader(System.IO.BinaryWriter bw,CacheChangeType opcode,string rpath){
      bw.Write(TMPFILE_MAGIC); // 4
      bw.Write(System.DateTime.Now.Ticks); // 8
      bw.Write((int)opcode); // 4
      bw.Write(rpath.Length); // 4
      foreach(char c in rpath)bw.Write((short)c); // 2*rpath.Length
    }
    static int WCacheGetHeaderSize(string rpath){
      return 4+8+4+4+2*rpath.Length;
    }

    /// <summary>
    /// 操作に関する付加的な情報をファイルストリームに書き込みます。
    /// この関数は CreateWCache 関数の処理の最中に呼び出されます。
    /// </summary>
    /// <param name="bw">情報を書き込む先の BinaryWriter を指定します。</param>
    /// <param name="param">CreateWCache に渡されたオブジェクトを指定します。</param>
    protected virtual void WCacheWriteContent(System.IO.BinaryWriter bw,object param){}

    protected void CreateWCache(object param){
      this.tmpfilepath=WCacheTmpname(fsid);
      using(System.IO.FileStream stream=System.IO.File.Open(tmpfilepath,System.IO.FileMode.CreateNew,System.IO.FileAccess.Write))
      using(System.IO.BinaryWriter bw=new System.IO.BinaryWriter(stream)){
        WCacheWriteHeader(bw,this.opcode,this.rpath);

        this.WCacheWriteContent(bw,param);
      }
    }

    protected virtual void WCacheReadContent(byte[] buff,int buffOffset,int offset,int length){
      using(System.IO.FileStream stream=System.IO.File.OpenRead(tmpfilepath)){
        offset+=WCacheGetHeaderSize(this.rpath);
        stream.Seek(offset,System.IO.SeekOrigin.Begin);
        stream.Read(buff,buffOffset,length);
      }
    }

    internal virtual void Clear(){
      if(System.IO.File.Exists(this.tmpfilepath))
        System.IO.File.Delete(this.tmpfilepath);
    }
    //========================================================================
    //  仮想状態構築
    //------------------------------------------------------------------------
    /// <summary>
    /// 操作後の path に対する操作が、操作前のどの path に対する操作に対応するかを返します。
    /// </summary>
    /// <param name="path">
    /// 操作後の path を指定します。
    /// 対応する操作前の path を返します。
    /// 例えば、この操作でファイルを A から B に移動した場合、
    /// path="B" を指定すると path="A" が返されます。
    /// </param>
    /// <returns>
    /// この操作が path に及ぼす影響に対応する戻り値を返します。
    /// 例1: この操作によって path にファイルが存在しない事が保証される場合には、PathRemove を返します。
    /// 例えば、path に存在していたファイルが削除される場合や、
    /// path から path2 へファイルを移動する場合がこれに相当します。
    /// 例2: また、この操作でファイル path が新しく作成され、既存のファイルが削除される場合は、PathCreateNew を返します。
    /// 例3: ファイルが存在していなかった時にファイル path が新しく作成される場合、PathCreate を返します。
    /// 例4: ファイルが存在していた場合にファイル path に対して変更が加えられる場合に PathChange を返します。
    /// 例5: ファイル path に対して (path が移動によって変化する事を除いて) 何の影響も持たない場合には OK を返します。
    /// </returns>
    protected internal virtual CacheChangeTraceStep PathTrace(ref string path){
      return CacheChangeTraceStep.Trivial;
    }
    /// <summary>
    /// この操作によって生起するファイル情報の変化をファイル path について再現します。
    /// </summary>
    /// <example>
    /// 例えば、この操作によってファイル A が作成される場合、
    /// targetPath="A" の時に fi に新規に作成されるファイルの情報を返します。
    /// また、この操作によってファイル A の日付が変更される場合、
    /// targetPath="A" の時に fi の日付情報を変更します。
    /// path に指定された物が変更の対象とならないファイルの場合は何も実行しません。
    /// </example>
    /// <param name="path">ファイル名を指定します。</param>
    /// <param name="fi">ファイル情報を指定します。</param>
    protected internal virtual void AfterGetFileInfo(string path,ref FileInfo fi){}
    /// <summary>
    /// この操作によって生起するファイルリスト情報の変化をディレクトリ path について再現します。
    /// </summary>
    /// <param name="path">ディレクトリ名を指定します。</param>
    /// <param name="list">ファイルリストを指定します。</param>
    protected internal virtual void AfterGetFileList(string path,ref Gen::IEnumerable<FileInfo> list){}
    /// <summary>
    /// ReadData の直前に呼び出され、
    /// basic.ReadData を用いて読み込まなければならないデータの範囲を修正します。
    /// </summary>
    /// <example>
    /// 例えば、この操作によって範囲 100-200 に data が書き込まれた場合、
    /// 100-200 はこのインスタンスが内容を持っているので basic.ReadData を用いて読み取る必要はありません。
    /// 従って、range.Remove(100,200) がこの関数内で呼び出されます。
    /// </example>
    /// <param name="path">読み出すファイルのパスを指定します。
    /// この変更操作が登録された時点でのファイルパスです。</param>
    /// <param name="range">読み取る必要があるファイル内のデータ領域を指定します。</param>
    protected internal virtual void BeforeReadData(string path,CacheDataRange range){}
    /// <summary>
    /// マスター FsBasic に対する ReadData の後に呼び出されます、
    /// この操作によって起こる変更を読み出されたデータに施します。
    /// </summary>
    /// <param name="path">読み出されたファイルのパスを指定します。
    /// この変更操作が登録された時点でのファイルパスです。</param>
    /// <param name="offset">ファイル内の読み取り開始位置を指定します。</param>
    /// <param name="buff">読み出されたデータの書込先を指定します。</param>
    /// <param name="buffOffset">読み出されたデータの書込開始位置を指定します。</param>
    /// <param name="length">読み出されたデータの長さを指定します。
    /// (実際には BeforeReadData によって除外された領域は読み出されず、
    /// buff には何も書き込まれていないかも知れません。)</param>
    protected internal virtual void AfterReadData(string path,long offset,byte[] buff,int buffOffset,int length){}
    //========================================================================
    //  変更適用
    //------------------------------------------------------------------------
    internal abstract int DoUpdate(CachedFsBasic cbasic);
  }

  class CacheChange_WriteData:CacheChange{
    readonly long offset;
    readonly int length;

    class InitParam{
      public byte[] buff;
      public int buffOffset;
      public int length;
      public InitParam(byte[] buff,int buffOffset,int length){
        this.buff=buff;
        this.buffOffset=buffOffset;
        this.length=length;
      }
    }

    public CacheChange_WriteData(string fsid,string rpath,long offset,byte[] buff,int buffOffset,int length)
      :base(fsid,rpath,CacheChangeType.WriteData)
    {
      System.Diagnostics.Debug.Assert(buff!=null&&0<=buffOffset&&buffOffset+length<=buff.Length);
      this.offset=offset;
      this.length=length;
      this.CreateWCache(new InitParam(buff,buffOffset,length));
    }

    const int FILE_BUFF_OFFSET=8+4;
    protected override void WCacheWriteContent(System.IO.BinaryWriter bw,object param){
      InitParam p=(InitParam)param;

      bw.Write(offset); // 8
      bw.Write(length); // 4
      for(int i=p.buffOffset,iM=p.buffOffset+length;i<iM;i++)bw.Write(p.buff[i]);
    }

    //------------------------------------------------------------------------
    protected internal override CacheChangeTraceStep PathTrace(ref string path) {
      if(path==this.Path)
        return CacheChangeTraceStep.PathCreate;
      else if(path==this.ParentDirectory)
        return CacheChangeTraceStep.ListChange;
      else
        return CacheChangeTraceStep.Trivial;
    }

    FileInfo CreateNewFileInfo(long filesize){
      FileInfo fi=FileInfo.CreateNewFile(
        this.Path,System.IO.Path.GetFileName(this.Path),this.OperationTime);
      fi.Length=filesize;
      return fi;
    }
    protected internal override void AfterGetFileInfo(string path,ref FileInfo fi) {
      if(path==this.Path){
        long minFilesize=this.offset+this.length;

        if(fi!=null){
          fi=new FileInfo(fi);
          fi.LastWriteTime=this.OperationTime;
          if(fi.Length<minFilesize)
            fi.Length=minFilesize;
        }else{
          fi=CreateNewFileInfo(minFilesize);
        }
      }
    }

    protected internal override void AfterGetFileList(string path,ref System.Collections.Generic.IEnumerable<FileInfo> list){
      if(path==this.ParentDirectory)
        list=CreateFileList(list);
    }
    private Gen::IEnumerable<FileInfo> CreateFileList(Gen::IEnumerable<FileInfo> list){
      long minFilesize=this.offset+this.length;
      string name=System.IO.Path.GetFileName(this.Path);
      bool fProcessed=false;

      foreach(FileInfo fi in list){
        if(fi.Name==name){
          FileInfo fi2=fi;
          AfterGetFileInfo(this.Path,ref fi2);
          yield return fi2;
          fProcessed=true;
        }else{
          yield return fi;
        }
      }

      if(!fProcessed)
        yield return CreateNewFileInfo(minFilesize);
    }

    protected internal override void BeforeReadData(string path,CacheDataRange range) {
      if(path==this.Path)
        range.Remove(this.offset,this.offset+this.length);
    }
    protected internal override void AfterReadData(string path,long offset,byte[] buff,int buffOffset,int length) {
      if(path==this.Path){
        offset-=this.offset;
        if(offset+length<=0||this.length<=offset)return;

        int _offset=(int)offset; // -length < offset < this.length
        if(length>this.length-_offset)
          length=this.length-_offset;

        if(_offset<0){
          buffOffset+=-_offset;
          length-=-_offset;
          _offset=0;
        }

        this.WCacheReadContent(buff,buffOffset,FILE_BUFF_OFFSET+_offset,length);
      }
    }

    object sync_clear=new object();
    bool clear_done=false;
    internal override void Clear(){
      if(clear_done)return;
      lock(sync_clear){
        if(clear_done)return;
        base.Clear();
      }
    }

    internal override int DoUpdate(CachedFsBasic cbasic){
      int len=this.length;
      byte[] buff;

      if(clear_done)return 0;
      lock(sync_clear){
        if(clear_done)return 0;

        buff=new byte[len];
        this.WCacheReadContent(buff,0,FILE_BUFF_OFFSET,length);
      }

      return cbasic.UpdateWriteData(this.OperationTime,buff,0,this.Path,this.offset,ref len);
    }
  }
  class CacheChange_SetFileInfo:CacheChange{
    class SetFileInfoData{
      public SetFileInfoFlags flags;
      public FileInfo finfo;
      public SetFileInfoData(SetFileInfoFlags flags,FileInfo finfo){
        this.flags=flags;
        this.finfo=new FileInfo(finfo);
      }
    }

    readonly SetFileInfoData data;

    public CacheChange_SetFileInfo(string fsid,string rpath,FileInfo finfo,SetFileInfoFlags flags)
      :base(fsid,rpath,CacheChangeType.SetFileInfo)
    {
      this.data=new SetFileInfoData(flags,finfo);
      this.CreateWCache(null);
    }

    protected override void WCacheWriteContent(System.IO.BinaryWriter bw,object param) {
      bw.Write((int)data.flags);
      bw.Write(data.finfo.Length);
      bw.Write(data.finfo.CreationTime.Ticks);
      bw.Write(data.finfo.LastWriteTime.Ticks);
      bw.Write(data.finfo.CreationTime.Ticks);
    }

    //------------------------------------------------------------------------
    protected internal override CacheChangeTraceStep PathTrace(ref string path) {
      if(path==this.Path)
        return CacheChangeTraceStep.PathChange;
      else if(path==this.ParentDirectory)
        return CacheChangeTraceStep.ListChange;
      else
        return CacheChangeTraceStep.Trivial;
    }
    protected internal override void AfterGetFileInfo(string path,ref FileInfo fi) {
      if(fi!=null&&path==this.Path){
        fi=new FileInfo(fi);
        if((data.flags&SetFileInfoFlags.SetFileSize)!=0)
          fi.Length=data.finfo.Length;

        if((data.flags&SetFileInfoFlags.SetFileMTime)!=0)
          if(data.finfo.LastWriteTime!=System.DateTime.MinValue)
            fi.LastWriteTime=data.finfo.LastWriteTime;
        if((data.flags&SetFileInfoFlags.SetFileATime)!=0)
          if(data.finfo.LastAccessTime!=System.DateTime.MinValue)
            fi.LastAccessTime=data.finfo.LastAccessTime;
        if((data.flags&SetFileInfoFlags.SetFileCTime)!=0)
          if(data.finfo.CreationTime!=System.DateTime.MinValue)
            fi.CreationTime=data.finfo.CreationTime;
      }
    }

    protected internal override void AfterGetFileList(string path,ref System.Collections.Generic.IEnumerable<FileInfo> list) {
      if(path==this.ParentDirectory)
        list=CreateFileList_Mod(list);
    }
    private Gen::IEnumerable<FileInfo> CreateFileList_Mod(Gen::IEnumerable<FileInfo> list){
      string name=System.IO.Path.GetFileName(this.Path);

      foreach(FileInfo fi in list){
        if(fi.Name==name){
          FileInfo fi2=fi;
          AfterGetFileInfo(this.Path,ref fi2);
          yield return fi2;
        }else{
          yield return fi;
        }
      }
    }

    protected internal override void BeforeReadData(string path,CacheDataRange range){
      if((data.flags&SetFileInfoFlags.SetFileSize)==0){
        if(path==this.Path)
          range.Remove(data.finfo.Length,long.MaxValue);
      }
    }
    protected internal override void AfterReadData(string path,long offset,byte[] buff,int buffOffset,int length) {
      if((data.flags&SetFileInfoFlags.SetFileSize)==0)return;
      if(path==this.Path){
        offset-=data.finfo.Length;
        if(offset+length<=0)return;

        if(offset<0){
          int delta=(int)(-offset); // -length < offset < 0
          buffOffset+=delta;
          length-=delta;
          offset=0;
        }

        System.Array.Clear(buff,buffOffset,length);
      }
    }

    internal override int DoUpdate(CachedFsBasic cbasic) {
      return cbasic.UpdateSetFileInfo(this.OperationTime,this.Path,this.data.finfo,this.data.flags);
    }
  }
  class CacheChange_MoveFile:CacheChange{
    string destinationPath;
    bool f_overwrite;
    FileInfo finfo;

    string DestinationDirectory{
      get{return CachedFsBasic.GetParentDirectory(this.destinationPath);}
    }

    public CacheChange_MoveFile(string fsid,string rpath,string rpathd,FileInfo finfo,bool f_overwrite)
      :base(fsid,rpath,CacheChangeType.MoveFile)
    {
      this.destinationPath=rpathd;
      this.f_overwrite=f_overwrite;
      this.finfo=new FileInfo(this.destinationPath,System.IO.Path.GetFileName(this.destinationPath),finfo);
      this.CreateWCache(null);
    }

    protected override void WCacheWriteContent(System.IO.BinaryWriter bw,object param) {
      bw.Write(f_overwrite);
      bw.Write(destinationPath.Length);
      foreach(char c in destinationPath)bw.Write((short)c);
    }

    //------------------------------------------------------------------------
    protected internal override CacheChangeTraceStep PathTrace(ref string path) {
      if(CachedFsBasic.IsDescendantOrSelf(path,this.Path))
        return CacheChangeTraceStep.PathRemoved;
      else if(CachedFsBasic.IsDescendantOrSelf(path,this.destinationPath)){
        path=this.Path+path.Substring(this.destinationPath.Length);
        return CacheChangeTraceStep.Trivial;
      }else if(path==this.ParentDirectory||path==this.DestinationDirectory)
        return CacheChangeTraceStep.ListChange;
      else
        return CacheChangeTraceStep.Trivial;
    }
    protected internal override void AfterGetFileList(string path,ref System.Collections.Generic.IEnumerable<FileInfo> list){
      bool f1=path==this.ParentDirectory;
      bool f2=path==CachedFsBasic.GetParentDirectory(this.destinationPath);
      if(f1){
        if(f2)
          list=CreateFileList_Moved(list);
        else
          list=CreateFileList_Left(list);
      }else{
        if(f2)
          list=CreateFileList_Reached(list);
      }
    }

    private Gen::IEnumerable<FileInfo> CreateFileList_Moved(Gen::IEnumerable<FileInfo> list){
      string name1=System.IO.Path.GetFileName(this.Path);
      string name2=System.IO.Path.GetFileName(this.destinationPath);

      foreach(FileInfo fi in list){
        if(fi.Name!=name2&&fi.Name!=name1)
          yield return fi;
      }

      yield return finfo;
      _todo.CacheFsBasicToDo("現在の FileInfo を取得する。");
      //FileInfo fi2;
      //if(basic.GetFileInfo(basic.ResolvePath(this.Path),out fi2)==0)
      //  yield return fi2;
    }
    private Gen::IEnumerable<FileInfo> CreateFileList_Reached(Gen::IEnumerable<FileInfo> list){
      string name=System.IO.Path.GetFileName(this.destinationPath);

      foreach(FileInfo fi in list){
        if(fi.Name!=name)
          yield return fi;
      }

      yield return finfo;
      _todo.CacheFsBasicToDo("現在の FileInfo を取得する。");
    }
    private Gen::IEnumerable<FileInfo> CreateFileList_Left(Gen::IEnumerable<FileInfo> list){
      string name=System.IO.Path.GetFileName(this.Path);

      foreach(FileInfo fi in list){
        if(fi.Name!=name)
          yield return fi;
      }
    }

    internal override int DoUpdate(CachedFsBasic cbasic) {
      return cbasic.UpdateMoveFile(this.OperationTime,this.Path,this.destinationPath);
    }
  }
  class CacheChange_CreateDirectory:CacheChange{
    public CacheChange_CreateDirectory(string fsid,string rpath)
      :base(fsid,rpath,CacheChangeType.CreateDirectory)
    {this.CreateWCache(null);}

    //------------------------------------------------------------------------
    protected internal override CacheChangeTraceStep PathTrace(ref string path) {
      if(path==this.Path)
        return CacheChangeTraceStep.PathCreate;
      else if(path==this.ParentDirectory)
        return CacheChangeTraceStep.ListChange;
      else
        return CacheChangeTraceStep.Trivial;
    }

    FileInfo CreateNewFileInfo(){
      return FileInfo.CreateNewDirectory(
        this.Path,System.IO.Path.GetFileName(this.Path),this.OperationTime);
    }

    protected internal override void AfterGetFileInfo(string path,ref FileInfo fi) {
      if(fi==null&&path==this.Path)
        fi=CreateNewFileInfo();
    }

    protected internal override void AfterGetFileList(string path,ref Gen::IEnumerable<FileInfo> list){
      if(list==null){
        if(path==this.Path)
          list=this.CreateFileList_New();
      }else{
        if(path==CachedFsBasic.GetParentDirectory(this.Path))
          list=this.CreateFileList_Added(list);
      }
    }
    private Gen::IEnumerable<FileInfo> CreateFileList_New(){yield break;}
    private Gen::IEnumerable<FileInfo> CreateFileList_Added(Gen::IEnumerable<FileInfo> list){
      string name=System.IO.Path.GetFileName(this.Path);
      bool fProcessed=false;
      foreach(FileInfo fi in list){
        if(fi.Name==name)
          fProcessed=true;
        yield return fi;
      }

      if(!fProcessed)
        yield return CreateNewFileInfo();
    }

    internal override int DoUpdate(CachedFsBasic cbasic) {
      return cbasic.UpdateCreateDirectory(this.OperationTime,this.Path);
    }
  }
  class CacheChange_RemoveDirectory:CacheChange{
    public CacheChange_RemoveDirectory(string fsid,string rpath)
      :base(fsid,rpath,CacheChangeType.RemoveDirectory)
    {this.CreateWCache(null);}

    protected internal override CacheChangeTraceStep PathTrace(ref string path) {
      if(CachedFsBasic.IsDescendantOrSelf(path,this.Path))
        return CacheChangeTraceStep.PathRemoved;
      else if(path==this.ParentDirectory)
        return CacheChangeTraceStep.ListChange;
      else
        return CacheChangeTraceStep.Trivial;
    }

    protected internal override void AfterGetFileList(string path,ref Gen::IEnumerable<FileInfo> list){
      if(path==CachedFsBasic.GetParentDirectory(this.Path)){
        list=CreateFileList_Removed(list);
      }
    }
    private Gen::IEnumerable<FileInfo> CreateFileList_Removed(Gen::IEnumerable<FileInfo> list){
      string name=System.IO.Path.GetFileName(this.Path);
      foreach(FileInfo fi in list){
        if(fi.Name!=name||!fi.IsDirectory)
          yield return fi;
      }
    }

    internal override int DoUpdate(CachedFsBasic cbasic) {
      return cbasic.UpdateRemoveDirectory(this.OperationTime,this.Path);
    }
  }
  class CacheChange_CreateFile:CacheChange{
    public CacheChange_CreateFile(string fsid,string rpath)
      :base(fsid,rpath,CacheChangeType.CreateFile)
    {this.CreateWCache(null);}

    FileInfo CreateNewFileInfo(){
      return FileInfo.CreateNewFile(
        this.Path,System.IO.Path.GetFileName(this.Path),this.OperationTime);
    }

    protected internal override CacheChangeTraceStep PathTrace(ref string path){
      if(path==this.Path){
        // 既存ファイルが存在しても、必ず操作で削除するので。
        return CacheChangeTraceStep.PathCreateNew;
      }else if(path==this.ParentDirectory)
        return CacheChangeTraceStep.ListChange;
      else
        return CacheChangeTraceStep.Trivial;
    }

    protected internal override void AfterGetFileInfo(string path,ref FileInfo fi) {
      if(path==this.Path){
        fi=this.CreateNewFileInfo();
        //if(fi==null){
        //  fi=this.CreateNewFileInfo();
        //}else{
        //  fi=new FileInfo(fi);
        //  fi.LastWriteTime=this.OperationTime;
        //  fi.Length=0;
        //}
      }
    }

    protected internal override void AfterGetFileList(string path,ref System.Collections.Generic.IEnumerable<FileInfo> list) {
      if(path==CachedFsBasic.GetParentDirectory(this.Path))
        list=CreateFileList_Mod(list);
    }
    private Gen::IEnumerable<FileInfo> CreateFileList_Mod(Gen::IEnumerable<FileInfo> list){
      string name=System.IO.Path.GetFileName(this.Path);
      bool fProcessed=false;

      foreach(FileInfo fi in list){
        if(fi.Name==name){
          FileInfo fi2=fi;
          AfterGetFileInfo(this.Path,ref fi2);
          yield return fi2;
          fProcessed=true;
        }else{
          yield return fi;
        }
      }

      if(!fProcessed)
        yield return CreateNewFileInfo();
    }

    internal override int DoUpdate(CachedFsBasic cbasic) {
      return cbasic.UpdateCreateFile(this.OperationTime,this.Path);
    }
  }
  class CacheChange_RemoveFile:CacheChange{
    public CacheChange_RemoveFile(string fsid,string rpath)
      :base(fsid,rpath,CacheChangeType.RemoveFile)
    {this.CreateWCache(null);}

    protected internal override CacheChangeTraceStep PathTrace(ref string path) {
      if(path==this.Path)
        return CacheChangeTraceStep.PathRemoved;
      else if(path==this.ParentDirectory)
        return CacheChangeTraceStep.ListChange;
      else
        return CacheChangeTraceStep.Trivial;
    }

    protected internal override void AfterGetFileList(string path,ref Gen::IEnumerable<FileInfo> list){
      if(path==this.ParentDirectory){
        list=CreateFileList_Removed(list);
      }
    }
    private Gen::IEnumerable<FileInfo> CreateFileList_Removed(Gen::IEnumerable<FileInfo> list){
      string name=System.IO.Path.GetFileName(this.Path);
      foreach(FileInfo fi in list){
        if(fi.Name!=name||fi.IsDirectory)
          yield return fi;
      }
    }

    internal override int DoUpdate(CachedFsBasic cbasic) {
      return cbasic.UpdateRemoveFile(this.OperationTime,this.Path);
    }
  }
  #endregion


  class CacheDataRange{
    //readonly Gen::SortedList<long,long> data=new Gen::SortedList<long,long>();

    readonly Gen::List<long> list=new Gen::List<long>();

    void AddRemoveRange(long start,long end,int mod){
      if(start>=end)return;

      int istart=list.BinarySearch(start);
      if(istart<0)
        istart=~istart;
      else if(istart%2==mod)
        istart++;

      int iend=list.BinarySearch(end);
      if(iend<0)
        iend=~iend;
      else if(iend%2==mod)
        iend++;

      if(iend>istart)
        list.RemoveRange(istart,iend-istart);
      if(iend%2==mod)
        list.Insert(istart,end);
      if(istart%2==mod)
        list.Insert(istart,start);
    }
    /// <summary>
    /// 領域を追加します。
    /// </summary>
    /// <param name="start">領域の開始位置を指定します。</param>
    /// <param name="end">領域の末端位置を指定します。</param>
    public void Add(long start,long end){
      AddRemoveRange(start,end,0);
    }
    /// <summary>
    /// 領域を削除します。
    /// </summary>
    /// <param name="start">領域の開始位置を指定します。</param>
    /// <param name="end">領域の末端位置を指定します。</param>
    public void Remove(long start,long end){
      AddRemoveRange(start,end,1);
    }
    /// <summary>
    /// 領域の境界位置を保持するリストを取得します。
    /// </summary>
    public Gen::List<long> List{
      get{return list;}
    }
    public bool IsEmpty{
      get{return list.Count==0;}
    }
    public long CommonStart{
      get{return list.Count==0?0:list[0];}
    }
    public long CommonEnd{
      get{return list.Count==0?0:list[list.Count-1];}
    }

    // Debug Code
    //
    //static readonly CacheDataRange inst=new CacheDataRange();
    //public static string tAdd(long start,long end){
    //  inst.Add(start,end);
    //  System.Text.StringBuilder b=new System.Text.StringBuilder();
    //  b.Append("debug");
    //  for(int i=0;i<inst.list.Count/2;i++){
    //    b.AppendFormat(" {0}-{1}",inst.list[2*i],inst.list[2*i+1]);
    //  }
    //  return b.ToString();
    //}
    //public static string tRem(long start,long end){
    //  inst.Remove(start,end);
    //  System.Text.StringBuilder b=new System.Text.StringBuilder();
    //  b.Append("debug");
    //  for(int i=0;i<inst.list.Count/2;i++){
    //    b.AppendFormat(" {0}-{1}",inst.list[2*i],inst.list[2*i+1]);
    //  }
    //  return b.ToString();
    //}
  }

  class CacheChangeTraceLock:System.IDisposable{
    readonly Gen::List<CacheChange> data;
    string[] paths;
    mwg.ReaderLock rlock;

    public CacheChangeTraceLock(System.Threading.ReaderWriterLock rwlock,Gen::List<CacheChange> data){
      this.data=data;
      this.rlock=new ReaderLock(rwlock);
    }

    //=========================================================================
    //  ReaderLock
    //-------------------------------------------------------------------------
    public void Release(){
      this.rlock.Dispose();
    }
    public void Dispose(){
      this.Release();
    }

    //=========================================================================
    //  変更操作の追跡
    //-------------------------------------------------------------------------
    //  ResultCode
    const int CTRUCT_FROM_MASTER=1;
    const int CTRUCT_CREATED    =2;
    const int CTRUCT_NOT_FOUND  =-1;
    int status=0;
    public bool IsNotFound{
      get{return status==CTRUCT_NOT_FOUND;}
    }
    public bool IsFromMaster{
      get{return status==CTRUCT_FROM_MASTER;}
    }
    //-------------------------------------------------------------------------
    int constructionStartIndex=int.MaxValue;
    /// <summary>
    /// 指定したパスに現在存在しているファイルの変更履歴を遡ります。
    /// </summary>
    /// <param name="path">追跡したいファイルのパスを指定します。
    /// マスター FsBasic の対応するファイルに対するパスを返します。
    /// 対応するパスが存在しない場合は null を返します。
    /// 例えば、ファイル A が B に移動された場合、
    /// path="B" として呼び出すと path="A" が返されます。
    /// path="A" として呼び出すと path=null が返されます。
    /// </param>
    /// <returns>path にファイルが存在しなかった場合には CTRUCT_NOT_FOUND を返します。
    /// マスター FsBasic に対応するファイルが見付かった場合には CTRUCT_FROM_MASTER を返します。
    /// マスター FsBasic に対応するファイルが見付からなかったが、
    /// それ以降の変更操作によってファイルが存在している場合には 0 以上の整数が返ります。
    /// </returns>
    internal void PathTrace(ref string path){
      this.paths=new string[data.Count];

      int creationIndex=data.Count; // ファイルの存在が保証される位置
      int changeIndex  =data.Count; // ファイルが存在していた時の最初の変更位置
      for(int index=data.Count-1;index>=0;index--){
        CacheChangeTraceStep type=data[index].PathTrace(ref path);
        this.paths[index]=path;
        switch(type){
          case CacheChangeTraceStep.PathRemoved:
            constructionStartIndex=creationIndex;
            if(creationIndex<data.Count){
              status=constructionStartIndex;
            }else{
              path=null;
              status=CTRUCT_NOT_FOUND;
            }
            return;
          case CacheChangeTraceStep.PathCreate:
            creationIndex=index;
            changeIndex=index;
            break;
          case CacheChangeTraceStep.PathChange:
          case CacheChangeTraceStep.ListChange:
            changeIndex=index;
            break;
          case CacheChangeTraceStep.PathCreateNew:
            path=null;
            constructionStartIndex=index;
            status=CTRUCT_CREATED;
            return;
        }
      }

      constructionStartIndex=changeIndex;
      status=CTRUCT_FROM_MASTER;
      return;
    }
    /// <summary>
    /// 直前の PathTrace の呼び出しに基づいて FileInfo を再構築します。
    /// </summary>
    /// <param name="constructionStartIndex"></param>
    /// <param name="fi">
    /// 現在のマスター FsBasic によって得られた FileInfo を指定します。
    /// PathTrace によってマスターに対応するファイルが存在しないと判定された時には null を指定します。
    /// 関数の呼び出し後には、再構築後の FileInfo が返されます。
    /// </param>
    /// <remarks><strong>lock(instance.SyncRoot) で直前の
    /// PathTrace 呼び出しと一緒に囲んで下さい。</strong>
    /// </remarks>
    internal void ReconstructFileInfo(ref FileInfo fi){
      for(int i=constructionStartIndex;i<data.Count;i++)
        data[i].AfterGetFileInfo(this.paths[i],ref fi);
    }
    /// <summary>
    /// 直前の PathTrace の呼び出しに基づいて FileInfo のリストを再構築します。
    /// </summary>
    /// <param name="i"></param>
    /// <param name="list">
    /// 現在のマスター FsBasic によって得られた FileInfo のリストを指定します。
    /// PathTrace によってマスターに対応するディレクトリが存在しないと判定された時には null を指定します。
    /// 関数の呼び出し後には、再構築後の FileInfo リストが返されます。
    /// </param>
    /// <remarks><strong>lock(instance.SyncRoot) で直前の
    /// PathTrace 呼び出しと一緒に囲んで下さい。</strong>
    /// </remarks>
    internal void ReconstructFileList(ref System.Collections.Generic.IEnumerable<FileInfo> list){
      for(int i=constructionStartIndex;i<data.Count;i++)
        data[i].AfterGetFileList(this.paths[i],ref list);
    }
    /// <summary>
    /// 直前の PathTrace の呼び出しに基づいて読み取る必要のあるデータ範囲を修正します。
    /// </summary>
    /// <param name="rpath"></param>
    /// <param name="readdataOffset"></param>
    /// <param name="readdataLength"></param>
    /// <remarks><strong>lock(instance.SyncRoot) で直前の
    /// PathTrace 呼び出しと一緒に囲んで下さい。</strong>
    /// </remarks>
    internal void BeforeReadData(ref long readdataOffset,ref int readdataLength){
      CacheDataRange range=new CacheDataRange();
      range.Add(readdataOffset,readdataOffset+readdataLength);
      for(int i=constructionStartIndex;i<data.Count;i++)
        data[i].BeforeReadData(this.paths[i],range);

      readdataOffset=range.CommonStart;
      readdataLength=(int)(range.CommonEnd-readdataOffset);
    }
    /// <summary>
    /// 直前の PathTrace の呼び出しに基づいてデータを再構築します。
    /// </summary>
    /// <param name="constructionStartIndex"></param>
    /// <param name="offset"></param>
    /// <param name="buff">データの書込先を指定します。
    /// PathTrace に続く BeforeReadData 呼び出しによってマスター FsBasic に必要なデータが
    /// 存在すると判定された場合には、予めマスター FsBasic によって読み取ったデータを書き込んでおきます。
    /// </param>
    /// <param name="buffOffset"></param>
    /// <param name="length"></param>
    /// <remarks><strong>lock(instance.SyncRoot) で直前の
    /// PathTrace 呼び出しと一緒に囲んで下さい。</strong>
    /// </remarks>
    internal void ReconstructReadData(long offset,byte[] buff,int buffOffset,int length) {
      for(int i=constructionStartIndex;i<data.Count;i++)
        data[i].AfterReadData(this.paths[i],offset,buff,buffOffset,length);
    }

  }

  class CacheChangeList{
    readonly Gen::List<CacheChange> data=new Gen::List<CacheChange>();

    readonly System.Threading.ReaderWriterLock rwlock=new System.Threading.ReaderWriterLock();
    public mwg.ReaderLock LockRead(){
      return new mwg.ReaderLock(this.rwlock);
    }
    public mwg.WriterLock LockWrite(){
      return new mwg.WriterLock(this.rwlock);
    }

    public CacheChangeList(){}

    public void Add(CacheChange change){
      using(this.LockWrite())
        this.data.Add(change);
    }
    public void Clear(){
      using(this.LockWrite()){
        foreach(CacheChange o in this.data)
          o.Clear();
      }
    }
    /// <summary>
    /// 指定したパスに対する操作を全て削除します。
    /// </summary>
    /// <param name="path">ファイル名を指定します。</param>
    /// <remarks>
    /// ファイルの削除に伴ってそのファイルに対する操作履歴を消す場合は、
    /// <strong>ファイルの削除が可能と判断される時のみに呼び出して下さい。</strong>
    /// ファイルの削除が失敗すると予想されるのにこれを実行すると、
    /// そのファイルに本来適用されるはずだった操作が実行されずに、
    /// 変更適用前のファイルだけが残ります。
    /// </remarks>
    public void ClearChangeOn(string path){
      using(this.LockRead()){
        for(int i=data.Count-1;i>=0;i--){
          switch(data[i].PathTrace(ref path)){
            case CacheChangeTraceStep.PathRemoved:
              // ここより昔の物は別のファイル (mv されて別のファイルになる物など)
              return;
            case CacheChangeTraceStep.PathChange:
            case CacheChangeTraceStep.PathCreate:
            case CacheChangeTraceStep.PathCreateNew:
              data[i].Clear();
              data.RemoveAt(i);
              break;
          }
        }
      }
    }

    /// <summary>
    /// LockRead 下で呼び出して下さい。
    /// </summary>
    /// <returns></returns>
    internal CacheChange GetFirst(){
      if(this.data.Count>0)
        return this.data[0];
      else
        return null;
    }
    /// <summary>
    /// LockRead 下で呼び出して下さい
    /// </summary>
    /// <param name="change">削除を意図している change インスタンスを指定します。</param>
    internal void ClearFirst(CacheChange change,ReaderLock rlock){
      if(this.data.Count==0||this.data[0]!=change)return;
      using(rlock.UpgradeToWriterLock()){
        if(this.data.Count==0||this.data[0]!=change)return;
        this.data.RemoveAt(0);
      }
      change.Clear();
    }

    internal void ClearFirst(CacheChange change){
      using(this.LockWrite()){
        if(this.data.Count==0||this.data[0]!=change)return;
        this.data.RemoveAt(0);
      }
      change.Clear();
    }

    //=========================================================================
    //  変更操作の追跡
    //-------------------------------------------------------------------------
    internal CacheChangeTraceLock LockTrace(){
      return new CacheChangeTraceLock(this.rwlock,this.data);
    }
  }

  [System.Flags]
  public enum CacheSetting{
    ReadCache       =0x01,

    WriteMASK       =0x30,
    WriteCache      =0x10,
    WriteOverlay    =0x20,
  }

  /// <summary>
  /// キャッシュされた操作を実現する FsBasic 実装です。
  /// </summary>
  public class CachedFsBasic:IFsBasic{
    readonly string fsid="__todo__";
    readonly IFsBasic basic;

    readonly bool f_write_cache=true;
    readonly bool f_write_update=false;
    readonly CacheChangeList dlist=null;
    readonly FileInfoCache ficache=new FileInfoCache();
    readonly System.Threading.Thread th_update=null;
    bool th_update_terminate=false;

    public CachedFsBasic(IFsBasic basic,CacheSetting setting){
      this.basic=basic;

      if((setting&CacheSetting.ReadCache)!=0){
        this.f_read_cache=true;
        this.thisL2=new FsL2Adapter(this);
      }else{
        this.f_read_cache=false;
      }

      switch(setting&CacheSetting.WriteMASK){
        case CacheSetting.WriteCache:
          this.f_write_cache=true;
          this.f_write_update=true;
          break;
        case CacheSetting.WriteOverlay:
          this.f_write_cache=true;
          this.f_write_update=false;
          break;
        default:
          this.f_write_cache=false;
          this.f_write_update=false;
          break;
      }
      if(this.f_write_cache){
        this.dlist=new CacheChangeList();
        if(this.f_write_update){
          this.th_update_terminate=false;
          this.th_update=new System.Threading.Thread(this.UpdateWorker);
          this.th_update.Name="<mwgvfs.CachedFsBasic.UpdateWorke> "+this.fsid;
          this.th_update.Start();
          System.Console.WriteLine("update thread start: "+this.th_update.Name);
        }
      }
    }
    public CachedFsBasic(IFsBasic basic)
      :this(basic,CacheSetting.WriteCache|CacheSetting.ReadCache)
    {}

    public bool IsReadOnly{
      get{
        if(f_write_cache&&!f_write_update)
          return false;
        return basic.IsReadOnly;
      }
    }
    public void Disconnect(){
      basic.Disconnect();
      this.store.Dispose();
    }
    public void Dispose(){
      this.Disconnect();
      this.th_update_terminate=true;

      if(f_write_cache&&!f_write_update)
        dlist.Clear();
    }
    public bool Reconnect() {
      return basic.Reconnect();
    }
    public int ReconnectCount {
      get { return basic.ReconnectCount; }
    }

    //========================================================================
    public string ResolvePath(string lpath){
      if(f_write_cache){
        if(lpath.Length>1&&(lpath[lpath.Length-1]=='\\'||lpath[lpath.Length-1]=='/'))
          return lpath.Substring(0,lpath.Length-1);
        else
          return lpath;
      }else{
        return basic.ResolvePath(lpath);
      }
    }
    public static bool IsDescendantOrSelf(string filepath,string directory){
      if(!filepath.StartsWith(directory))return false;
      if(filepath.Length==directory.Length)return true;
      return filepath[directory.Length]=='\\'||filepath[directory.Length]=='/';
    }
    public static string GetParentDirectory(string rpath){
      if(rpath.EndsWith("\\"))
        rpath=rpath.Substring(0,rpath.Length-1);
      return System.IO.Path.GetDirectoryName(rpath);
    }

    //========================================================================
    //  [cached.finfo] GetFileInfo/GetFileList
    //------------------------------------------------------------------------
    FileInfo GetFileInfoL2(string rpath){
      FileInfo fi=null;

      // rpath=this.ResolvePath(lpath);
      // tpath=this.PathRedirect(rpath);
      // bpath=this.basic.ResolvePath(tpath);

      // path resolve
      string tpath=rpath;
      using(CacheChangeTraceLock trace=dlist.LockTrace()){
        trace.PathTrace(ref tpath);
        if(trace.IsNotFound)
          return null;
        else if(trace.IsFromMaster)
          fi=basic.GetFileInfo(basic.ResolvePath(tpath));
        trace.ReconstructFileInfo(ref fi);
      }

      // result
      if(fi==null)
        return null;
      else
        return ModifyInternalPath(rpath,fi,System.DateTime.Now);
    }

    FileInfo ModifyInternalPath(string rpath,FileInfo fi,System.DateTime informationTime){
      FileInfo fi2=new FileInfo(rpath,System.IO.Path.GetFileName(rpath),fi);
      fi2.informationTime=informationTime;
      return fi2;
    }

    int GetFileListL2(string rpath,out Gen::IEnumerable<FileInfo> list) {
      list=null;
      int returnCode=0;

      string tpath=rpath;
      using(CacheChangeTraceLock trace=dlist.LockTrace()){
        trace.PathTrace(ref tpath);
        if(trace.IsNotFound)
          return -WinErrorCode.ERROR_FILE_NOT_FOUND;
        else if(trace.IsFromMaster)
          returnCode=basic.GetFileList(basic.ResolvePath(tpath),out list);
        trace.ReconstructFileList(ref list);
      }

      // result
      if(list==null)
        return returnCode!=0?returnCode:-WinErrorCode.ERROR_FILE_NOT_FOUND;
      list=ModifyInternalPath(rpath,list,System.DateTime.Now);
      return 0;
    }

    Gen::IEnumerable<FileInfo> ModifyInternalPath(string rpath,Gen::IEnumerable<FileInfo> list,System.DateTime informationTime){
      foreach(FileInfo fi in list){
        FileInfo fi2=new FileInfo(System.IO.Path.Combine(rpath,fi.Name),fi.Name,fi);
        fi2.informationTime=informationTime;
        yield return fi2;
      }
    }
    //------------------------------------------------------------------------
    public FileInfo GetFileInfo(string rpath){
      if(f_write_cache){
        System.DateTime now=System.DateTime.Now;
        FileInfo fi;
        if(this.ficache.TryGetFile(now,rpath,out fi))return fi;
        lock(ficache){ // (double-checked locking)
          if(this.ficache.TryGetFile(now,rpath,out fi))return fi;

          fi=GetFileInfoL2(rpath);
          if(fi!=null)
            ficache.SetFile(fi);
          else
            ficache.SetFileNotFound(rpath);
          return fi;
        }
        //return WCacheGetFileInfo(rpath);
      }else{
        return basic.GetFileInfo(rpath);
      }
    }

    public int GetFileList(string rpath,out Gen::IEnumerable<FileInfo> list) {
      if(f_write_cache){
        System.DateTime now=System.DateTime.Now;
        if(this.ficache.TryGetList(now,rpath,out list))
          return list!=null?0:-WinErrorCode.ERROR_FILE_NOT_FOUND;
        lock(ficache){ // (double-checked locking)
          if(this.ficache.TryGetList(now,rpath,out list))
            return list!=null?0:-WinErrorCode.ERROR_FILE_NOT_FOUND;

          int r=GetFileListL2(rpath,out list);
          if(r==0)
            ficache.SetList(rpath,list);
          else if(r==-WinErrorCode.ERROR_FILE_NOT_FOUND)
            ficache.SetList(rpath,null); // "ディレクトリが存在しない" という事をキャッシュ
          return r;
        }
        //return WCacheGetFileList(rpath,out list);
      }else{
        return basic.GetFileList(rpath,out list);
      }
    }
    //========================================================================
    //  [cached.readdata] ReadData/OpenFile/CloseFile
    //------------------------------------------------------------------------
    readonly bool f_read_cache=true;
    //------------------------------------------------------------------------
    //  [cached.readdata.L1]
#if READ_CACHE_L1
    readonly CacheBlockStore store=new CacheBlockStore();
    readonly Gen::Dictionary<string,CacheFile> files=new Gen::Dictionary<string,CacheFile>();
    int ReadDataL1(string rpath,byte[] buff,int buffOffset,string bpath,long offset,ref int length) {
      if(f_read_cache){
        CacheFile cache;
        if(!files.TryGetValue(rpath,out cache))
          return basic.ReadData(buff,buffOffset,bpath,offset,ref length);
        if(cache.name!=bpath){
          // リダイレクト先が変わった時、再生成
          System.Console.WriteLine("dbg: reinitialize cache");
          CloseFileL1(rpath,bpath);
          OpenFileL1(rpath,bpath);
        }

        cache.Update();
        return cache.Read(buff,buffOffset,offset,ref length);
      }else{
        return basic.ReadData(buff,buffOffset,bpath,offset,ref length);
      }
    }
    void OpenFileL1(string rpath,string bpath){
      if(f_read_cache){
        lock(files)if(!files.ContainsKey(rpath))
          files[rpath]=new CacheFile(bpath,basic,store);
        //output.Write(2,": fopen {0}",rpath);
      }

      basic.OpenFile(bpath);
    }
    public void OpenFile(string rpath){}
    void CloseFileL1(string rpath,string bpath){
      basic.CloseFile(bpath);

      if(f_read_cache){
        CacheFile file;
        lock(files)if(files.TryGetValue(rpath,out file)){
          file.Dispose();
          files.Remove(rpath);
        }
        //output.Write(2,": fclose {0}",rpath);
      }
    }
#else
    int ReadDataL1(string rpath,byte[] buff,int buffOffset,string bpath,long offset,ref int length) {
      return basic.ReadData(buff,buffOffset,bpath,offset,ref length);
    }
    void OpenFileL1(string rpath,string bpath){
      basic.OpenFile(bpath);
    }
    void CloseFileL1(string rpath,string bpath){
      basic.CloseFile(bpath);
    }
#endif

    //------------------------------------------------------------------------
    //  [cached.readdata.L2]
    int ReadDataL2(byte[] buff,int buffOffset,string rpath,long offset,ref int length) {
      if(f_write_cache){
        long end=offset+length;
        FileInfo fi=this.GetFileInfo(rpath);
        if(fi==null)
          return -WinErrorCode.ERROR_FILE_NOT_FOUND;

        if(offset>fi.Length){
          length=0;
          return -1;
        }else if(length>fi.Length-offset)
          length=(int)(fi.Length-offset);

        int returnCode=0;
        using(CacheChangeTraceLock trace=dlist.LockTrace()){
          trace.PathTrace(ref rpath);
          if(trace.IsNotFound)
            return -WinErrorCode.ERROR_FILE_NOT_FOUND;
          else if(trace.IsFromMaster){
            // 書込キャッシュにある領域は読まなくて OK
            long readOffset=offset;
            int readLength=length;
            trace.BeforeReadData(ref readOffset,ref readLength);

            if(readLength>0){
              // 読み取りと length 計算
              int resultLength=readLength;
              returnCode=this.ReadDataL1(rpath,buff,buffOffset,basic.ResolvePath(rpath),offset,ref resultLength);
              if(resultLength!=readLength)
                length=(int)(resultLength+readOffset-offset);
            }
          }

          trace.ReconstructReadData(offset,buff,buffOffset,length);
        }

        return returnCode;
      }else{
        return this.ReadDataL1(rpath,buff,buffOffset,rpath,offset,ref length);
      }
    }
    void OpenFileL2(string rpath){
      if(f_write_cache){
        using(CacheChangeTraceLock trace=dlist.LockTrace())
          trace.PathTrace(ref rpath);
        if(rpath!=null)
          this.OpenFileL1(rpath,basic.ResolvePath(rpath));
        //if(i==CacheChangeList.CTRUCT_NOT_FOUND)return;
        //this.OpenFileL1(rpath,basic.ResolvePath(rpath));
      }else{
        this.OpenFileL1(rpath,rpath);
      }
    }
    void CloseFileL2(string rpath){
      if(f_write_cache){
        using(CacheChangeTraceLock trace=dlist.LockTrace())
          trace.PathTrace(ref rpath);
        if(rpath!=null)
          this.CloseFileL1(rpath,basic.ResolvePath(rpath));
        //if(i==CacheChangeList.CTRUCT_NOT_FOUND)return; // OpenFile した後に RemoveFile したかも知れない
        //this.CloseFileL1(rpath,basic.ResolvePath(rpath));
      }else{
        this.CloseFileL1(rpath,rpath);
      }
    }

    //------------------------------------------------------------------------
    //  [cached.readdata.L3]
#if READ_CACHE_L1
    public int ReadData(byte[] buff,int buffOffset,string path,long offset,ref int length) {
      return this.ReadDataL2(buff,buffOffset,path,offset,ref length);
    }
    public void OpenFile(string rpath){
      this.OpenFileL2(rpath);
    }
    public void CloseFile(string rpath){
      this.CloseFileL2(rpath);
    }
#else
    readonly CacheBlockStore store=new CacheBlockStore();
    readonly Gen::Dictionary<string,CacheFile> files=new Gen::Dictionary<string,CacheFile>();
    readonly FsL2Adapter thisL2=null;

    class FsL2Adapter:IFsBasic{
      readonly CachedFsBasic self;
      public FsL2Adapter(CachedFsBasic self){this.self=self;}

      public FileInfo GetFileInfo(string rpath){return self.GetFileInfo(rpath);}
      public int ReadData(byte[] buff,int buffOffset,string path,long offset,ref int length){
        return self.ReadDataL2(buff,buffOffset,path,offset,ref length);
      }
      public void OpenFile(string rpath){
        self.OpenFileL2(rpath);
      }
      public void CloseFile(string rpath){
        self.CloseFileL2(rpath);
      }

      #region 未使用メンバ
      public bool IsReadOnly{get{return self.IsReadOnly;}}
      public void Disconnect(){self.Disconnect();}
      public bool Reconnect(){return self.Reconnect();}
      public int ReconnectCount{get{return self.ReconnectCount;}}
      public string ResolvePath(string lpath){return self.ResolvePath(lpath);}
      public int GetFileList(string path,out Gen::IEnumerable<FileInfo> list){return self.GetFileList(path,out list);}
      public int CreateDirectory(string rpath){return self.CreateDirectory(rpath);}
      public int RemoveDirectory(string rpath){return self.RemoveDirectory(rpath);}
      public int MoveFile(string rpaths,string rpathd){return self.MoveFile(rpaths,rpathd);}
      public int RemoveFile(string rpath){return self.RemoveFile(rpath);}
      public int SetFileInfo(string rpath,FileInfo finfo,SetFileInfoFlags flags){return self.SetFileInfo(rpath,finfo,flags);}
      public int CreateFile(string rpath){return self.CreateFile(rpath);}
      public int WriteData(byte[] buff,int buffOffset,string path,long offset,ref int length){
        return self.WriteData(buff,buffOffset,path,offset,ref length);
      }
      public void Dispose(){self.Dispose();}
      #endregion
    }

    public int ReadData(byte[] buff,int buffOffset,string rpath,long offset,ref int length) {
      if(f_read_cache){
        CacheFile cache;
        if(!files.TryGetValue(rpath,out cache))
          return this.ReadDataL2(buff,buffOffset,rpath,offset,ref length);

        cache.Update();
        return cache.Read(buff,buffOffset,offset,ref length);
      }else{
        return this.ReadDataL2(buff,buffOffset,rpath,offset,ref length);
      }
    }
    public void OpenFile(string rpath){
      if(f_read_cache){
        lock(files)if(!files.ContainsKey(rpath))
          files[rpath]=new CacheFile(rpath,thisL2,store);
      }

      this.OpenFileL2(rpath);
    }
    public void CloseFile(string rpath){
      this.CloseFileL2(rpath);

      if(f_read_cache){
        CacheFile file;
        lock(files)if(files.TryGetValue(rpath,out file)){
          file.Dispose();
          files.Remove(rpath);
        }
      }
    }
#endif

    //========================================================================
    //  [cached.destructive] 変更操作のキャッシュ追加
    //------------------------------------------------------------------------
    public int CreateDirectory(string rpath) {
      if(f_write_cache){
        {
          if(f_write_update&&basic.IsReadOnly)
            return -WinErrorCode.ERROR_WRITE_PROTECT;
          if(this.GetFileInfo(rpath)!=null)
            return -WinErrorCode.ERROR_ALREADY_EXISTS;
          FileInfo parent_info=this.GetFileInfo(GetParentDirectory(rpath));
          if(parent_info==null||!parent_info.IsDirectory)
            return -WinErrorCode.ERROR_PATH_NOT_FOUND;
          if(parent_info.IsReadOnly)
            return -WinErrorCode.ERROR_FILE_READ_ONLY;
          //■access check
        }

        dlist.Add(new CacheChange_CreateDirectory(fsid,rpath));
        ficache.ClearFile(rpath);
        ficache.ClearList(System.IO.Path.GetDirectoryName(rpath));
        return 0;
      }else{
        return basic.CreateDirectory(rpath);
      }
    }
    public int RemoveDirectory(string rpath){
      if(f_write_cache){
        {
          if(f_write_update&&basic.IsReadOnly)
            return -WinErrorCode.ERROR_WRITE_PROTECT;
          FileInfo finfo=this.GetFileInfo(rpath);
          if(finfo==null)
            return -WinErrorCode.ERROR_FILE_NOT_FOUND;
          if(!finfo.IsDirectory)
            return -WinErrorCode.ERROR_DIRECTORY; // 通常ファイル
          if(finfo.IsReadOnly)
            return -WinErrorCode.ERROR_FILE_READ_ONLY;
          Gen::IEnumerable<FileInfo> list;
          if(this.GetFileList(rpath,out list)==0)
            foreach(FileInfo fi in list)
              return -WinErrorCode.ERROR_DIR_NOT_EMPTY; // ディレクトリの中身が未だある。
          //■access check
        }

        dlist.Add(new CacheChange_RemoveDirectory(fsid,rpath));
        ficache.ClearFile(rpath);
        ficache.ClearList(System.IO.Path.GetDirectoryName(rpath));
        return 0;
      }else{
        return basic.RemoveDirectory(rpath);
      }
    }

    public int CreateFile(string rpath){
      if(f_write_cache){
        {
          FileInfo finfo=this.GetFileInfo(rpath);
          if(finfo!=null){
            if(finfo.IsDirectory||finfo.Length==0)
              return 0;
            else if(f_write_update&&basic.IsReadOnly)
              return -WinErrorCode.ERROR_WRITE_PROTECT;
            else if(f_write_update&&finfo.IsReadOnly)
              return -WinErrorCode.ERROR_FILE_READ_ONLY;
          }else{
            if(f_write_update&&basic.IsReadOnly)
              return -WinErrorCode.ERROR_WRITE_PROTECT;
            FileInfo parent_info=this.GetFileInfo(GetParentDirectory(rpath));
            if(parent_info==null||!parent_info.IsDirectory)
              return -WinErrorCode.ERROR_PATH_NOT_FOUND;
          }
          //■access check
        }

        dlist.Add(new CacheChange_CreateFile(fsid,rpath));
        ficache.ClearFile(rpath);
        ficache.ClearList(System.IO.Path.GetDirectoryName(rpath));
        return 0;
      }else{
        return basic.CreateFile(rpath);
      }
    }

    public int MoveFile(string rpaths,string rpathd) {
      if(f_write_cache){
        FileInfo sinfo;
        bool f_overwrite;
        {
          if(f_write_update&&basic.IsReadOnly)
            return -WinErrorCode.ERROR_WRITE_PROTECT;
          if(CachedFsBasic.IsDescendantOrSelf(rpathd,rpaths))
            return -WinErrorCode.ERROR_ACCESS_DENIED;
          sinfo=this.GetFileInfo(rpaths);
          if(sinfo==null)
            return -WinErrorCode.ERROR_FILE_NOT_FOUND;
          FileInfo dinfo=this.GetFileInfo(rpathd);
          
          if(dinfo!=null){
            if(dinfo.IsDirectory)
              return -WinErrorCode.ERROR_ALREADY_EXISTS;
            f_overwrite=true;
          }else{
            FileInfo dparent_info=this.GetFileInfo(GetParentDirectory(rpathd));
            if(dparent_info==null||!dparent_info.IsDirectory)
              return -WinErrorCode.ERROR_PATH_NOT_FOUND;
            f_overwrite=false;
          }
          // (ファイル上書き確認は呼出元で処理する筈)
        }

        dlist.Add(new CacheChange_MoveFile(fsid,rpaths,rpathd,sinfo,f_overwrite));
        ficache.ClearFile(rpaths);
        ficache.ClearFile(rpathd);
        ficache.ClearList(System.IO.Path.GetDirectoryName(rpaths));
        ficache.ClearList(System.IO.Path.GetDirectoryName(rpathd));
        return 0;
      }else{
        return basic.MoveFile(rpaths,rpathd);
      }
    }

    public int RemoveFile(string rpath) {
      if(f_write_cache){
        {
          if(f_write_update&&basic.IsReadOnly)
            return -WinErrorCode.ERROR_WRITE_PROTECT;
          FileInfo fi=this.GetFileInfo(rpath);
          if(fi==null)
            return -WinErrorCode.ERROR_FILE_NOT_FOUND;
          if(fi.IsDirectory)
            return -WinErrorCode.ERROR_DIRECTORY;
          if(f_write_update&&fi.IsReadOnly)
            return -WinErrorCode.ERROR_FILE_READ_ONLY;
          //■access check
        }

        using(dlist.LockWrite()){
          dlist.ClearChangeOn(rpath);
          dlist.Add(new CacheChange_RemoveFile(fsid,rpath));
        }
        ficache.ClearFile(rpath);
        ficache.ClearList(System.IO.Path.GetDirectoryName(rpath));
        return 0;
      }else{
        return basic.RemoveFile(rpath);
      }
    }

    public int SetFileInfo(string rpath,FileInfo finfo,SetFileInfoFlags flags) {
      if(f_write_cache){
        {
          if(f_write_update&&basic.IsReadOnly)
            return -WinErrorCode.ERROR_WRITE_PROTECT;
          FileInfo fi=this.GetFileInfo(rpath);
          if(fi==null)
            return -WinErrorCode.ERROR_FILE_NOT_FOUND;
          if(f_write_update&&fi.IsReadOnly)
            return -WinErrorCode.ERROR_FILE_READ_ONLY;
          //■permission check
        }

        dlist.Add(new CacheChange_SetFileInfo(fsid,rpath,finfo,flags));
        ficache.ApplyFile_SetFileInfo(rpath,finfo,flags);
        ficache.ApplyList_SetFileInfo(GetParentDirectory(rpath),System.IO.Path.GetFileName(rpath),finfo,flags);
        return 0;
      }else{
        return basic.SetFileInfo(rpath,finfo,flags);
      }
    }

    public int WriteData(byte[] buff,int buffOffset,string path,long offset,ref int length) {
      if(f_write_cache){
        {
          if(f_write_update&&basic.IsReadOnly){
            length=0;return -WinErrorCode.ERROR_WRITE_PROTECT;
          }
          FileInfo finfo=this.GetFileInfo(path);
          if(finfo!=null){
            if(finfo.IsDirectory){
              length=0;return -WinErrorCode.ERROR_DIRECTORY_NOT_SUPPORTED;
            }
            if(f_write_update&&finfo.IsReadOnly){
              length=0;return -WinErrorCode.ERROR_FILE_READ_ONLY;
            }
          }else{
            FileInfo parent_info=this.GetFileInfo(GetParentDirectory(path));
            if(parent_info==null||!parent_info.IsDirectory){
              length=0;return -WinErrorCode.ERROR_PATH_NOT_FOUND;
            }
            if(f_write_update&&parent_info.IsReadOnly){
              length=0;return -WinErrorCode.ERROR_FILE_READ_ONLY;
            }
          }
          //■permission check
        }

        if(length>=buff.Length-buffOffset)
          length=buff.Length-buffOffset;
        if(buffOffset<0||length<0){
          length=0;
          return -1;
        }

        dlist.Add(new CacheChange_WriteData(fsid,path,offset,buff,buffOffset,length));
        string lname=System.IO.Path.GetFileName(path);
        ficache.ApplyFile_WriteData(path,lname,offset+length);
        ficache.ApplyList_WriteData(GetParentDirectory(path),lname,offset+length);
        return 0;
      }else{
        return basic.WriteData(buff,buffOffset,path,offset,ref length);
      }
    }
    //========================================================================
    //  [cached.update] 変更操作の更新
    //------------------------------------------------------------------------
    void BackupFile(string bpath){
      FileInfo finfo=basic.GetFileInfo(bpath);
      if(finfo!=null){
        string bpath2=bpath+"~";
        this.BackupFile(bpath);
        basic.MoveFile(bpath,bpath2);
      }
    }
    internal int UpdateCreateDirectory(System.DateTime operationTime,string rpath){
      if(!f_write_update)throw new System.InvalidProgramException();
      string bpath=basic.ResolvePath(rpath);

      {
        if(basic.IsReadOnly)
          return -WinErrorCode.ERROR_WRITE_PROTECT;
        FileInfo finfo=basic.GetFileInfo(bpath);
        if(finfo!=null){
          if(finfo.IsDirectory)return 0;
          this.BackupFile(bpath);
        }else{
          string parent_bpath=basic.ResolvePath(GetParentDirectory(rpath));
          FileInfo parent_info=basic.GetFileInfo(parent_bpath);
          if(parent_info==null)
            return -WinErrorCode.ERROR_PATH_NOT_FOUND;
          if(parent_info.IsReadOnly)
            return -WinErrorCode.ERROR_FILE_READ_ONLY;
        }
      }

      return basic.CreateDirectory(bpath);
    }
    internal int UpdateRemoveDirectory(System.DateTime operationTime,string rpath){
      if(!f_write_update)throw new System.InvalidProgramException();
      string bpath=basic.ResolvePath(rpath);

      {
        if(basic.IsReadOnly)
          return -WinErrorCode.ERROR_WRITE_PROTECT;
        FileInfo finfo=basic.GetFileInfo(bpath);
        if(finfo==null)
          return 0;
        if(!finfo.IsDirectory)
          return -WinErrorCode.ERROR_DIRECTORY;
        if(finfo.IsReadOnly)
          return -WinErrorCode.ERROR_FILE_READ_ONLY;
        if(finfo.LastWriteTime>operationTime)
          return -WinErrorCode.ERROR_ACCESS_DENIED; // 齟齬

        Gen::IEnumerable<FileInfo> list;
        if(basic.GetFileList(bpath,out list)==0)
          foreach(FileInfo fi in list)
            return -WinErrorCode.ERROR_DIR_NOT_EMPTY; // ディレクトリの中身が未だある。
      }

      return basic.RemoveDirectory(bpath);
    }
    internal int UpdateCreateFile(System.DateTime operationTime,string rpath){
      if(!f_write_update)throw new System.InvalidProgramException();
      string bpath=basic.ResolvePath(rpath);

      {
        FileInfo finfo=basic.GetFileInfo(bpath);
        if(finfo!=null){
          if(finfo.IsDirectory||finfo.Length==0)
            return 0;
          else if(basic.IsReadOnly)
            return -WinErrorCode.ERROR_WRITE_PROTECT;
          else if(finfo.IsReadOnly)
            return -WinErrorCode.ERROR_FILE_READ_ONLY;
          else if(finfo.LastWriteTime>operationTime)
            this.BackupFile(bpath);
        }else{
          if(basic.IsReadOnly)
            return -WinErrorCode.ERROR_WRITE_PROTECT;

          string parent_rpath=GetParentDirectory(rpath);
          string parent_bpath=basic.ResolvePath(parent_rpath);
          if(basic.GetFileInfo(parent_bpath)==null)
            return -WinErrorCode.ERROR_PATH_NOT_FOUND; // ■CreateDirectory する?
        }
      }

      return basic.CreateFile(bpath);
    }
    internal int UpdateMoveFile(System.DateTime operationTime,string rpaths,string rpathd) {
      if(!f_write_update)throw new System.InvalidProgramException();
      string bpaths=basic.ResolvePath(rpaths);
      string bpathd=basic.ResolvePath(rpathd);

      {
        if(basic.IsReadOnly)
          return -WinErrorCode.ERROR_WRITE_PROTECT;
        if(CachedFsBasic.IsDescendantOrSelf(rpathd,rpaths))
          return -WinErrorCode.ERROR_ACCESS_DENIED;

        FileInfo sinfo=basic.GetFileInfo(bpaths);
        if(sinfo==null)
          return -WinErrorCode.ERROR_FILE_NOT_FOUND;

        FileInfo dinfo=basic.GetFileInfo(bpathd);
        if(dinfo!=null){
          if(dinfo.IsDirectory||dinfo.LastWriteTime>operationTime)
            this.BackupFile(bpathd);
          // それ以外は上書き
        }else{
          string dparent_rpath=GetParentDirectory(rpathd);
          string dparent_bpath=basic.ResolvePath(dparent_rpath);
          FileInfo dparent_info=basic.GetFileInfo(dparent_bpath);
          if(dparent_info==null||!dparent_info.IsDirectory)
            return -WinErrorCode.ERROR_PATH_NOT_FOUND; // ■CreateDirectory する?
        }
      }

      return basic.MoveFile(bpaths,bpathd);
    }
    internal int UpdateRemoveFile(System.DateTime operationTime,string rpath) {
      if(!f_write_update)throw new System.InvalidProgramException();
      string bpath=basic.ResolvePath(rpath);

      {
        if(basic.IsReadOnly)
          return -WinErrorCode.ERROR_WRITE_PROTECT;
        FileInfo fi=basic.GetFileInfo(bpath);
        if(fi==null)
          return -WinErrorCode.ERROR_FILE_NOT_FOUND;
        if(fi.IsDirectory)
          return -WinErrorCode.ERROR_DIRECTORY;
        if(fi.IsReadOnly)
          return -WinErrorCode.ERROR_FILE_READ_ONLY;
        if(fi.LastWriteTime>operationTime)
          this.BackupFile(bpath);
      }

      return basic.RemoveFile(bpath);
    }
    internal int UpdateSetFileInfo(System.DateTime operationTIme,string rpath,FileInfo finfo,SetFileInfoFlags flags) {
      if(!f_write_update)throw new System.InvalidProgramException();
      string bpath=basic.ResolvePath(rpath);

      {
        if(basic.IsReadOnly)
          return -WinErrorCode.ERROR_WRITE_PROTECT;
        FileInfo fi=basic.GetFileInfo(bpath);
        if(fi==null)
          return -WinErrorCode.ERROR_FILE_NOT_FOUND;
        if(fi.IsReadOnly)
          return -WinErrorCode.ERROR_FILE_READ_ONLY;
        if(fi.LastWriteTime>operationTIme)
          return -WinErrorCode.ERROR_ACCESS_DENIED;
        //■permission check
      }

      return basic.SetFileInfo(bpath,finfo,flags);
    }

    internal int UpdateWriteData(System.DateTime operationTime,byte[] buff,int buffOffset,string rpath,long offset,ref int length) {
      if(!f_write_update)throw new System.InvalidProgramException();
      string bpath=basic.ResolvePath(rpath);

      {
        if(basic.IsReadOnly){
          length=0;return -WinErrorCode.ERROR_WRITE_PROTECT;
        }
        FileInfo finfo=basic.GetFileInfo(bpath);
        if(finfo!=null){
          if(finfo.IsDirectory||finfo.IsReadOnly)
            this.BackupFile(bpath);
          else if(finfo.LastWriteTime>operationTime){
            /* ■こういう時はどうするべきか?
             *  ---------------------------------------------------------------
             *  A ファイルをコピーする (BackupCopyFile)
             *    然し、コピーは直接はサポートされていない。SFTP ですらコピーはサポートしない。
             *    巨大なファイルの場合は特に致命的。
             *  B 知らない振りをして上書きする
             *    仕方ないので、現状はこちら。
             *  ---------------------------------------------------------------
             *  更に、この様な状況は変更の衝突が無くても、
             *  前回の書込時刻の誤差で今回の書込時刻を越える事があるといった理由で、
             *  この条件が満たされる可能性もある。
             */
          }
        }else{
          string parent_rpath=GetParentDirectory(rpath);
          string parent_bpath=basic.ResolvePath(parent_rpath);
          FileInfo parent_info=this.GetFileInfo(parent_bpath);
          if(parent_info==null||!parent_info.IsDirectory){
            length=0;return -WinErrorCode.ERROR_PATH_NOT_FOUND;//■CreateDirectory?
          }
          if(parent_info.IsReadOnly){
            length=0;return -WinErrorCode.ERROR_FILE_READ_ONLY;//■BackupFile(parent_bpath) & CreateDirectory?
          }
        }
      }

      int code=basic.WriteData(buff,buffOffset,bpath,offset,ref length);

      FileInfo fi=basic.GetFileInfo(bpath);
      if(fi!=null){
        fi.LastWriteTime=operationTime;
        basic.SetFileInfo(bpath,fi,SetFileInfoFlags.SetFileMTime);
      }
      /*  ■SetFileTIme はどうするべきか? 毎回すると非効率的。
       *  でもそうしないと書込が終了した途端に、表示される書込日時が変わる事になる。
       *  Makefile とかでのコンパイルもやり直しになる。
       *  その他、後続の書込動作の最終書込時刻確認でも問題を生じる。
       *  仕様がないので実行するしかない。
       *  これは、今後書込操作の結合で改善すると期待する。
       */

      return code;
    }

    void UpdateWorker(){
      while(!this.th_update_terminate){
        try{
          CacheChange change;

          using(ReaderLock rlock=dlist.LockRead())
            change=dlist.GetFirst();

          if(change!=null){
            int code=-WinErrorCode.ERROR_ACCESS_DENIED;

            int count=basic.ReconnectCount;
            for(;;){
              try{
                code=change.DoUpdate(this);
              }catch(FsBasicReturnException e){
                code=e.returnCode;
              }catch(System.Exception e){
                System.Console.WriteLine("CachedFsBasic.UpdateWorker()! Exception\n{0}",e);
                code=FsBasicUtil.ErrorRequestReconnection;
              }

              if(code!=FsBasicUtil.ErrorRequestReconnection)break;

              code=-1;
              do{
                if(count--==0)
                  goto giveup;
              }while(!basic.Reconnect());
            }
          giveup:
            if(code!=0)
              System.Console.WriteLine("CachedFsBasic.UpdateWorker()! Return Code {0}",code);

            dlist.ClearFirst(change);
          }else
            System.Threading.Thread.Sleep(500);

        }catch{}
      }
    }

  }
}

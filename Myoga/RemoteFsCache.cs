using Gen=System.Collections.Generic;
using System.Text;
using Interop=System.Runtime.InteropServices;
using Dokan;
using jsch=Tamir.SharpSsh.jsch;
using java=Tamir.SharpSsh.java;
using AfhPath=afh.Application.Path;

namespace mwg.Sshfs{
	// キャッシュ機構のインターフェイス
	interface IDataCache:System.IDisposable{
		void Open(string name);
		void Close(string name);
		int ReadData(byte[] buff,int buffOffset,string name,long offset,ref int length);
		int WriteData(byte[] buff,int buffOffset,string name,long offset,ref int length);
	}
	//****************************************************************************
	//	キャッシュなし
	//****************************************************************************
	class RemoteNoCache:IDataCache{
		protected IRemoteFsOperation operation;
		protected SshfsMessage message;

		public RemoteNoCache(IRemoteFsOperation operation,SshfsMessage message){
			this.operation=operation;
			this.message=message;
		}
		//--------------------------------------------------------------------------
		// データの読み取り
		public virtual int ReadData(byte[] buff,int buffOffset,string name,long offset,ref int length){
			return operation.ReadData(buff,buffOffset,name,offset,ref length);
		}
		//--------------------------------------------------------------------------
		// データの書込
		public virtual int WriteData(byte[] buff,int buffOffset,string name,long offset,ref int length){
			return operation.WriteData(buff,buffOffset,name,offset,ref length);
		}
		//--------------------------------------------------------------------------
		public virtual void Open(string name){
			message.Write(2,"$ fopen {0}",name);
			return;
		}
		public virtual void Close(string name){
			message.Write(2,"$ fclose {0}",name);
			return;
		}
		public virtual void Dispose(){}
	}
	//****************************************************************************
	//	簡易キャッシュ
	//****************************************************************************
	class RemoteEasyCache:RemoteNoCache{
		class cache_t{
			public long offset=0;
			public int length=0;
			public byte[] buff=new byte[0];
			public System.DateTime mtime;
		}
		Gen::Dictionary<string,cache_t> dic_cache
			=new Gen::Dictionary<string,cache_t>();

		public RemoteEasyCache(IRemoteFsOperation operation,SshfsMessage message):base(operation,message){}

		public override void Open(string name){
			dic_cache[name]=new cache_t();
			base.Open(name);
		}
		public override void Close(string name){
			dic_cache.Remove(name);
			base.Close(name);
		}
		public override int ReadData(byte[] buff,int buffOffset,string name,long offset,ref int length){
			cache_t cache;
			if(!dic_cache.TryGetValue(name,out cache)){
				return base.ReadData(buff,buffOffset,name,offset,ref length);
			}

			System.DateTime mtime=operation.FileGetMTime(name);

			if(cache.mtime==mtime)lock(cache){
				int offsetInCache=(int)(offset-cache.offset);
				if(offsetInCache>=0&&offset+length<=cache.offset+cache.buff.Length){
					if(length>cache.length-offsetInCache)
						length=cache.length-offsetInCache;
					System.Array.Copy(cache.buff,offsetInCache,buff,buffOffset,length);
					return 0;
				}
			}

			long coff=offset&~0x0FFF;
			long cend=offset+length+0xFFF&~0xFFF;
			int len=(int)(cend-coff);
			byte[] nbuff=new byte[len];

			int ret=base.ReadData(nbuff,0,name,coff,ref len);
			if(ret!=0)return ret;
			{
				int offsetInCache=(int)(offset-coff);
				if(len-offsetInCache<length)
					length=len-offsetInCache;
				System.Array.Copy(nbuff,offset-coff,buff,buffOffset,length);
			}

			lock(cache){
				cache.buff=nbuff;
				cache.offset=coff;
				cache.length=len;
				cache.mtime=mtime;
			}
			return 0;
		}
	}
	//****************************************************************************
	//	Disk Cache
	//****************************************************************************
	class RemoteDiskCache:RemoteNoCache,System.IDisposable{

		public RemoteDiskCache(IRemoteFsOperation operation,SshfsMessage message)
			:base(operation,message)
		{
			this.InitCacheFile();
    }

    #region IFsBasic Adapter
    class FsBasic_RemoteFsOperation_Adapter:Dokan.DokanOperations,IRemoteFsOperation{
      readonly Dokan.DokanOperations operation;
      readonly mwg.mwgvfs.gvfs.IFsBasic basic;
      readonly SshfsMessage output;
      public FsBasic_RemoteFsOperation_Adapter(
        mwg.mwgvfs.gvfs.GvfsOperation operation,
        mwg.mwgvfs.gvfs.IFsBasic basic,
        SshfsMessage message
      ){
        this.operation=operation;
        this.basic=basic;
        this.output=message;
      }

      //--------------------------------------------------------------------------
      void IRemoteFsOperation.SetMTime(string rpath,System.DateTime mtime){
        mwg.mwgvfs.gvfs.FileInfo finfo=basic.GetFileInfo(rpath);
        finfo.LastWriteTime=mtime;
        basic.SetFileInfo(rpath,finfo,mwg.mwgvfs.gvfs.SetFileInfoFlags.SetFileMTime);
      }
      System.DateTime IRemoteFsOperation.FileGetMTime(string rpath){
        mwg.mwgvfs.gvfs.FileInfo finfo=basic.GetFileInfo(rpath);
        return finfo.LastWriteTime;
      }
      long IRemoteFsOperation.FileGetSize(string rpath){
        mwg.mwgvfs.gvfs.FileInfo finfo=basic.GetFileInfo(rpath);
        return finfo.Length;
      }
      string IRemoteFsOperation.ResolvePath(string lpath){
        return basic.ResolvePath(lpath);
      }
      int IRemoteFsOperation.ReadData(byte[] buff,int buffOffset,string path,long offset,ref int length){
        try{
          return basic.ReadData(buff,buffOffset,path,offset,ref length);
        }catch(System.Exception e1){
          return output.ReportErr(e1);
        }
      }
      int IRemoteFsOperation.WriteData(byte[] buff,int buffOffset,string path,long offset,ref int length){
        try{
          return basic.WriteData(buff,buffOffset,path,offset,ref length);
        }catch(System.Exception e1){
          return output.ReportErr(e1);
        }
      }
      //--------------------------------------------------------------------------

      #region DokanOperations メンバ
      public int Cleanup(string filename,DokanFileInfo info) {
        return this.operation.Cleanup(filename,info);
      }

      public int CloseFile(string filename,DokanFileInfo info) {
        return this.operation.CloseFile(filename,info);
      }

      public int CreateDirectory(string filename,DokanFileInfo info) {
        return this.operation.CreateDirectory(filename,info);
      }

      public int CreateFile(string filename,System.IO.FileAccess access,System.IO.FileShare share,System.IO.FileMode mode,System.IO.FileOptions options,DokanFileInfo info) {
        return this.operation.CreateFile(filename,access,share,mode,options,info);
      }

      public int DeleteDirectory(string filename,DokanFileInfo info) {
        return this.operation.DeleteDirectory(filename,info);
      }

      public int DeleteFile(string filename,DokanFileInfo info) {
        return this.operation.DeleteFile(filename,info);
      }

      public int FindFiles(string filename,System.Collections.ArrayList files,DokanFileInfo info) {
        return this.operation.FindFiles(filename,files,info);
      }

      public int FlushFileBuffers(string filename,DokanFileInfo info) {
        return this.operation.FlushFileBuffers(filename,info);
      }

      public int GetDiskFreeSpace(ref ulong freeBytesAvailable,ref ulong totalBytes,ref ulong totalFreeBytes,DokanFileInfo info) {
        return this.operation.GetDiskFreeSpace(ref freeBytesAvailable,ref totalBytes,ref totalFreeBytes,info);
      }

      public int GetFileInformation(string filename,FileInformation fileinfo,DokanFileInfo info) {
        return this.operation.GetFileInformation(filename,fileinfo,info);
      }

      public int LockFile(string filename,long offset,long length,DokanFileInfo info) {
        return this.operation.LockFile(filename,offset,length,info);
      }

      public int MoveFile(string filename,string newname,bool replace,DokanFileInfo info) {
        return this.operation.MoveFile(filename,newname,replace,info);
      }

      public int OpenDirectory(string filename,DokanFileInfo info) {
        return this.operation.OpenDirectory(filename,info);
      }

      public int ReadFile(string filename,byte[] buffer,ref uint readBytes,long offset,DokanFileInfo info) {
        return this.operation.ReadFile(filename,buffer,ref readBytes,offset,info);
      }

      public int SetAllocationSize(string filename,long length,DokanFileInfo info) {
        return this.operation.SetAllocationSize(filename,length,info);
      }

      public int SetEndOfFile(string filename,long length,DokanFileInfo info) {
        return this.operation.SetEndOfFile(filename,length,info);
      }

      public int SetFileAttributes(string filename,System.IO.FileAttributes attr,DokanFileInfo info) {
        return this.operation.SetFileAttributes(filename,attr,info);
      }

      public int SetFileTime(string filename,System.DateTime ctime,System.DateTime atime,System.DateTime mtime,DokanFileInfo info) {
        return this.operation.SetFileTime(filename,ctime,atime,mtime,info);
      }

      public int UnlockFile(string filename,long offset,long length,DokanFileInfo info) {
        return this.operation.UnlockFile(filename,offset,length,info);
      }

      public int Unmount(DokanFileInfo info) {
        return this.operation.Unmount(info);
      }

      public int WriteFile(string filename,byte[] buffer,ref uint writtenBytes,long offset,DokanFileInfo info) {
        return this.operation.WriteFile(filename,buffer,ref writtenBytes,offset,info);
      }

      #endregion
    }
    public RemoteDiskCache(
      mwg.mwgvfs.gvfs.GvfsOperation operation,
      mwg.mwgvfs.gvfs.IFsBasic basic,SshfsMessage message)
      :base(new FsBasic_RemoteFsOperation_Adapter(operation,basic,message),message)
    {
      this.InitCacheFile();
    }
    #endregion
		//==========================================================================
		// 一時ファイル
		//==========================================================================
		static System.Random rand=new System.Random();
		string temppath;
		System.IO.FileStream tempostr;
		private void InitCacheFile(){
			// cachedir
			string cachedir=AfhPath.Combine(AfhPath.ExecutableDirectory,"cache");
			AfhPath.EnsureDirectoryExistence(ref cachedir);

			// temppath
			temppath=AfhPath.Combine(cachedir,rand.Next(0x1000000).ToString("X6"));
			temppath=AfhPath.GetAvailablePath(temppath,"tmp");
			this.tempostr=System.IO.File.Open(
				temppath,
				System.IO.FileMode.CreateNew,
				System.IO.FileAccess.ReadWrite
				);
		}
		public override void Dispose(){
			lock(files){
				foreach(File file in files.Values)
					file.Dispose();
				files.Clear();
			}

			lock(this.tempostr){
				this.tempostr.Close();
				this.tempostr=null;
				System.IO.File.Delete(this.temppath);
				this.temppath=null;
			}

			base.Dispose();
		}
		//==========================================================================
		// 一時ファイル上のブロック管理
		//==========================================================================
		const int BLKSIZE=0x1000;
		const int BLKMASK=0x0FFF;
		int nblocks=0;
		Gen::List<block_t> freeblocks=new Gen::List<block_t>();

		class block_t{
			long position;
			System.DateTime dt;
			public block_t(int iblk){
				this.position=iblk*(long)BLKSIZE;
				this.dt=System.DateTime.MinValue;
			}

			public void ReadCache(RemoteDiskCache cache,byte[] buff,int buffOffset,int offset,int length){
				lock(cache.tempostr){
					cache.tempostr.Seek(this.position+offset,System.IO.SeekOrigin.Begin);
					length=cache.tempostr.Read(buff,buffOffset,length);
				}
			}
			public void WriteCache(RemoteDiskCache cache,byte[] buff,int buffOffset,int offset,int length){
				lock(cache.tempostr){
					cache.tempostr.Seek(this.position+offset,System.IO.SeekOrigin.Begin);
					cache.tempostr.Write(buff,buffOffset,length);
				}
				this.dt=System.DateTime.Now;
			}

			public System.DateTime UpdateTime{
				get{return this.dt;}
			}
		}
		block_t AllocBlock(){
			lock(freeblocks){
				if(freeblocks.Count==0)
					return new block_t(nblocks++);
				block_t ret=freeblocks[freeblocks.Count-1];
				freeblocks.RemoveAt(freeblocks.Count-1);
				return ret;
			}
		}
		void FreeBlock(block_t block){
			lock(freeblocks){
				freeblocks.Add(block);
			}
		}
		//==========================================================================
		//	実装
		//==========================================================================
		public override void Open(string name){
			lock(files)if(!files.ContainsKey(name))
				files[name]=new File(name,this);
			base.Open(name);
		}
		public override void Close(string name){
			File file;
			lock(files)if(files.TryGetValue(name,out file)){
				file.Dispose();
				files.Remove(name);
			}
			base.Close(name);
		}
		public override int ReadData(byte[] buff,int buffOffset,string name,long offset,ref int length){
			File cache;
			if(!files.TryGetValue(name,out cache)){
				return base.ReadData(buff,buffOffset,name,offset,ref length);
			}

			cache.Update();
			cache.Read(buff,buffOffset,offset,ref length);
			return 0;
		}
		//==========================================================================
		//	File
		//==========================================================================
		Gen::Dictionary<string,File> files=new Gen::Dictionary<string,File>();
		class File :System.IDisposable{
			public string name;
			public RemoteDiskCache parent;
			public Gen::Dictionary<int,block_t> map=new Gen::Dictionary<int,block_t>();
			public System.DateTime mtime=System.DateTime.MinValue;
			public long filesize;

			public File(string name,RemoteDiskCache parent){
				this.name=name;
				this.parent=parent;
			}
			public void Dispose(){
				this.Clear();
			}
			public void Clear(){
				lock(map){
					foreach(block_t b in this.map.Values)
						parent.FreeBlock(b);
					map.Clear();
				}
			}
			public void Update(){
				bool dirty=false;
				lock(map){
					long filesize=parent.operation.FileGetSize(name);
					if(this.filesize!=filesize){
						dirty=true;
						this.filesize=filesize;
					}

					System.DateTime mtime=parent.operation.FileGetMTime(this.name);
					if(this.mtime!=mtime){
						dirty=true;
						this.mtime=mtime;
					}

					if(dirty)this.Clear();
				}
			}
			//FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF
			//	Read
			//========================================================================
#if true
			public int Read(byte[] buff,int buffOffset,long offset,ref int rlength){
				// ファイルサイズ制限
				if(offset+rlength>filesize)rlength=(int)(filesize-offset);

				ReadParams p=new ReadParams(this,buff,buffOffset,offset,rlength);
				lock(map){
					if(p.triml_cached()){
						rlength=p.ReadLength;
						return 0;
					}

					p.accel_seq_access();
					p.trimr_cached();

					// データ取得
					p.download_data();
					rlength=p.ReadLength;
					return 0;
				}
			}
			struct ReadParams{
				File file;
				byte[] buff;

				int  data_off_buff;
				long data_off_file;
				int  data_len     ;

				int boff;
				int bend;

				int read_len;
				int read_len_tail;

				public ReadParams(File file,byte[] buff,int buffOffset,long offset,int rlength){
					this.file=file;
					this.buff=buff;

					this.data_off_buff = buffOffset;
					this.data_off_file = offset;
					this.data_len      = rlength;
					this.boff=(int)(data_off_file/BLKSIZE);
					this.bend=required_blocks(data_off_file+rlength);

					this.read_len=0;
					this.read_len_tail=0;
				}
				//----------------------------------------------------------------------
				/// <summary>
				/// 既にキャッシュにデータを持っているかどうか先頭から調べ、
				/// キャッシュがあった場合にはデータをバッファにコピーします。
				/// </summary>
				/// <returns>全てのデータがキャッシュにあり読取が完了した場合に true を返します。</returns>
				public bool triml_cached(){
					// 既にデータを持っている部分は処理
					block_t b;
					while(boff<bend&&file.map.TryGetValue(boff,out b)){
						int copy_off_block=(int)(data_off_file-boff*(long)BLKSIZE);
						int copy_len=BLKSIZE-copy_off_block;
						if(copy_len>data_len)copy_len=data_len;
						b.ReadCache(file.parent,buff,data_off_buff,copy_off_block,copy_len);

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
				/// データを順番に舐めていっている事を検知して、まとめて先読みを実行するようにする。
				/// </summary>
				public void accel_seq_access(){
					const int MIN_BLK=2;   // まとめ読み最小ブロック数
					const int MAX_BLK=16;  // まとめ読み最大ブロック数
					const int FACTOR =2;   // 最近読んだ長さの 1/FACTOR を先読みするか
					const int TIMESPAN=5;  // 「最近」とは何秒前か

					if(data_len>=MAX_BLK*BLKSIZE&&boff<FACTOR*MIN_BLK)return;


					int nblk; // 少なくともまとめて先読みしたい量
					{
						System.DateTime time_thresh=System.DateTime.Now.AddSeconds(-TIMESPAN);

						int i=boff;
						int iM=boff-MAX_BLK*FACTOR;
						if(iM<0)iM=0;
						while(i>=iM){
							block_t b;
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
				public void trimr_cached(){
					// 末端からも処理
					block_t b;
					while(boff<bend-1&&file.map.TryGetValue(bend-1,out b)){
						int copy_off_data=(int)((bend-1)*(long)BLKSIZE-data_off_file);
						int copy_off_buff=data_off_buff+copy_off_data;
						int copy_len=(int)(data_len-copy_off_data);
						if(copy_len>0){
							b.ReadCache(file.parent,buff,copy_off_buff,0,copy_len);

							data_len-=copy_len;
							read_len_tail+=copy_len;
						}
						bend--;
					}
				}
				public void download_data(){
					long load_off_file=boff*(long)BLKSIZE;
					long load_end_file=bend*(long)BLKSIZE;
					if(load_end_file>file.filesize)
						load_end_file=file.filesize;
					int load_len=(int)(load_end_file-load_off_file);
					byte[] nbuff=new byte[BLKSIZE*required_blocks(load_len)];

					int ret=file.parent.operation.ReadData(nbuff,0,file.name,load_off_file,ref load_len);
					file.update_cache(nbuff,boff,load_len);

					// 結果書込
					int data_off_nbuff=(int)(data_off_file-load_off_file);
					if(load_len-data_off_nbuff<data_len){
						data_len=load_len-data_off_nbuff;
					}else{
						read_len+=read_len_tail;
					}
					read_len+=data_len;
					System.Array.Copy(nbuff,data_off_nbuff,buff,data_off_buff,data_len);
				}
				//----------------------------------------------------------------------
				public int ReadLength{
					get{return read_len;}
				}
				static int required_blocks(long size){
					return (int)((size+BLKMASK)/BLKSIZE);
				}
			}
#else
			public int Read(byte[] buff,int buffOffset,long offset,ref int rlength){
				// ファイルサイズ制限
				if(offset+rlength>filesize)rlength=(int)(filesize-offset);

				int  data_off_buff = buffOffset;
				long data_off_file = offset;
				int  data_len      = rlength;
				int boff=(int)(data_off_file/BLKSIZE);
				int bend=(int)((data_off_file+rlength+BLKMASK)/BLKSIZE);

				rlength=0;
				int rlength_tail=0;

				lock(map){
					// 既にデータを持っている部分は処理
					block_t b;
					while(boff<bend&&map.TryGetValue(boff,out b)){
						int copy_off_block=(int)(data_off_file-boff*(long)BLKSIZE);
						int copy_len=BLKSIZE-copy_off_block;
						if(copy_len>data_len)copy_len=data_len;
						b.ReadCache(parent,buff,data_off_buff,copy_off_block,copy_len);

						// 更新
						data_off_buff+=copy_len;
						data_off_file+=copy_len;
						data_len-=copy_len;
						rlength+=copy_len;
						boff++;
					}

					// 末端からも処理
					while(boff<bend-1&&map.TryGetValue(bend-1,out b)){
						int copy_off_data=(int)((bend-1)*BLKSIZE-data_off_file);
						int copy_off_buff=data_off_buff+copy_off_data;
						int copy_len=(int)(data_len-copy_off_data);
						b.ReadCache(parent,buff,copy_off_buff,0,copy_len);

						data_len-=copy_len;
						rlength_tail+=copy_len;
						bend--;
					}
					if(boff==bend)return 0;

					// データ取得
					{
						long load_off_file=boff*BLKSIZE;
						long load_end_file=bend*BLKSIZE;
						int load_len=(int)(load_end_file-load_off_file);
						byte[] nbuff=new byte[load_len];

						int ret=parent.operation.ReadData(nbuff,0,this.name,load_off_file,ref load_len);
						this.update_cache(nbuff,boff,load_len);

						// 結果書込
						int data_off_nbuff=(int)(data_off_file-load_off_file);
						if(load_len-data_off_nbuff<data_len){
							data_len=load_len-data_off_nbuff;
						}else{
							rlength+=rlength_tail;
						}
						rlength+=data_len;
						System.Array.Copy(nbuff,data_off_nbuff,buff,data_off_buff,data_len);
						return 0;
					}
				}

				/*
				lock(cache){
					cache.buff=nbuff;
					cache.offset=coff;
					cache.length=length;
				}
				//*/
			}
#endif
			void update_cache(byte[] buffer,int boff,int len){
				long off=boff*BLKSIZE;
				long end=off+len;

				// どのブロックまで取得成功しているか
				int iM;
				if(end==filesize){
					iM=(int)((end+(BLKSIZE-1))/BLKSIZE);
				}else{
					iM=(int)(end/BLKSIZE);
				}

				for(int i=boff;i<iM;i++){
					block_t b;
					if(!map.TryGetValue(i,out b)){
						b=parent.AllocBlock();
						map.Add(i,b);
					}
					b.WriteCache(parent,buffer,(i-boff)*BLKSIZE,0,BLKSIZE);
				}
			}
		}
	}
}

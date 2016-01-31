using Dokan;
using System.IO;
using jsch=Tamir.SharpSsh.jsch;
using java=Tamir.SharpSsh.java;
using Gen=System.Collections.Generic;

using Forms=System.Windows.Forms;
namespace mwg.Sshfs{
	partial class SftpFsOperation:DokanOperations,IRemoteFsOperation{
		private ISftpAccount account;
		private ISshSession session;
		private IDataCache cache;
		private FileAutoCloser closer;

		public SftpFsOperation(ISshSession session,ISftpAccount account){
			this.session=session;
			this.stat_cache=new SftpStatCache(this);
			//this.cache=new RemoteEasyCache(this,session.Message);
			this.cache=new RemoteDiskCache(this,session.Message);
			this.closer=new FileAutoCloser(cache);
			this.account=account;

			// resolve_path
			string path_remote=account.ServerRoot;
			if(path_remote==null)path_remote="";
			if(path_remote[path_remote.Length-1]=='/')
				path_remote=path_remote.Substring(0,path_remote.Length-1);
			this.path_remote=path_remote;
		}
		public SftpFsOperation(SshUserData userdata,ISftpAccount account)
			:this(new SshSession(userdata,account),account)
		{}
		public ISshSession Session{
			get{return this.session;}
		}
		public string RootDirectory{
			get{return this.path_remote;}
		}
		//==========================================================================
		//	Settings
		//==========================================================================
		private string path_remote;
		internal SftpSymlink SymlinkTreatment{
			get{return account.SymlinkTreatment;}
		}
		//==========================================================================
		//	Utils
		//==========================================================================
		private SftpStatCache stat_cache;
		internal jsch::SftpATTRS stat(string path){
			return this.stat(path,true);
		}
		private Gen::List<SftpFileInfo> ls_parent(string path){
			if(path.Length>this.path_remote.Length+1)
				path=Unix.UnixPath.GetParentPath(path);
			return this.ls(path);
		}
		private jsch::SftpATTRS stat(string path,bool trace){
			jsch::SftpATTRS ret;
			if(this.stat_cache.TryGetAttr(path,out ret))return ret;

			this.ls_parent(path);
			if(this.stat_cache.TryGetAttr(path,out ret))return ret;

			session.Message.Write(1,"$ stat {0}",path);
			ret=session.Sftp.stat(path);
			if(trace)ResolveLink(path,ref ret,30);
			this.stat_cache.SetAttr(path,ret);
			return ret;
		}
		private void setStat(string path,jsch::SftpATTRS attr){
			session.Message.Write(1,"$ set stat {0}",path);
			session.Sftp.setStat(path,attr);
			this.stat_cache.SetAttr(path,attr);
		}
		private Gen::List<SftpFileInfo> ls(string path){
			if(path.Length>1&&path[path.Length-1]=='/')
				path=path.Substring(0,path.Length-1);

			Gen::List<SftpFileInfo> ret;
			if(this.stat_cache.TryGetList(path,out ret))return ret;
			lock(stat_cache){ // (double-checked locking)
				if(this.stat_cache.TryGetList(path,out ret))return ret;

				session.Message.Write(1,"$ ls {0}",path);
				java::util.Vector vec=session.Sftp.ls(path);
				return this.stat_cache.SetList(path,vec);
			}
		}
		//--------------------------------------------------------------------------
		private string ResolvePath(string filename){
			filename=filename.Replace('\\','/');
			if(filename.Length==0||filename[0]!='/')filename="/"+filename;
			return this.path_remote+filename;
		}
		private static bool IsAltStream(string filename){
			if(filename.IndexOf(':')<0)return false;
			string[] arr=filename.Split(new char[]{':'}, 2);
			return arr.Length==2&&arr[1].StartsWith("SSHFSProperty.");
		}
		//--------------------------------------------------------------------------
		enum FileType{
			NotExist,
			File,
			Directory,
			Link,
		}
		private FileType GetFileType(string path){
			bool mustbe_directory=false;
			if(path.Length>=2&&path[path.Length-1]=='/'){
				path=path.Substring(0,path.Length-1);
				mustbe_directory=true;
			}

			try{
				jsch::SftpATTRS attr;
				if(!this.stat_cache.TryGetAttr(path,out attr)){
					this.ls_parent(path);
					if(!this.stat_cache.TryGetAttr(path,out attr)){
						session.Message.Write(1,"! not found: {0}",path);
						return FileType.NotExist;
					}
				}

				if(attr.isDir())return FileType.Directory;
				if(attr.isLink())return FileType.Link;
				if(mustbe_directory)return FileType.NotExist;
				return FileType.File;
			}catch(jsch::SftpException e){
				session.Message.ReportErr(e);
				return FileType.NotExist;
			}
		}
		//--------------------------------------------------------------------------
		internal void ResolveLink(string path,ref jsch::SftpATTRS attrs,int hop){
			if(!attrs.isLink()||hop<=0)return;
			//System.Console.WriteLine("link {0}",path);
			if(path.EndsWith("/"))path=path.Substring(0,path.Length-1);

			// get target path
			string target=session.Sftp.readlink(path);
			if(target.Length==0)return;
			target=Unix.UnixPath.Combine(Unix.UnixPath.GetDirectoryPath(path),target);

			// get attribute
			attrs=this.stat(target,false);
			ResolveLink(target,ref attrs,hop-1);
		}
		//==========================================================================
		//	IRemoteFsOperations
		//==========================================================================
		#region IRemoteFsOperations メンバ
		void IRemoteFsOperation.SetMTime(string path,System.DateTime mtime) {
			jsch::SftpATTRS attr=this.stat(path);

			int asecs=attr.getATime();

			session.Message.Write(1,"$ touch {0} -md {1}",path,mtime);
			int msecs=Unix.UnixTime.DateTimeToUnixTime(mtime);

			attr.setACMODTIME(asecs,msecs);
			this.setStat(path,attr);
		}
		System.DateTime IRemoteFsOperation.FileGetMTime(string path){
			return Unix.UnixTime.UnixTimeToDateTime(this.stat(path).getMTime());
		}
		long IRemoteFsOperation.FileGetSize(string path){
			return this.stat(path).getSize();
		}
		string IRemoteFsOperation.ResolvePath(string filepath){
			return this.ResolvePath(filepath);
		}
		//--------------------------------------------------------------------------
		int IRemoteFsOperation.ReadData(byte[] buff,int buffOffset,string path,long offset,ref int length){
			try{
				ReadProgress monitor=new ReadProgress(offset+length);
				ReadDataStream dst=new ReadDataStream(buff,buffOffset);
				session.Message.Write(1,"$ get {0} {1:X}-{2:X}",path,offset,offset+length);
				session.Sftp.get(path,dst,monitor,jsch::ChannelSftp.RESUME,offset);
				length=dst.ReceivedBytes;
				return 0;
			}catch(System.Exception e1){
				return session.Message.ReportErr(e1);
			}
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
		int IRemoteFsOperation.WriteData(byte[] buff,int buffOffset,string path,long offset,ref int length){
			try{
				session.Message.Write(1,"$ put {0} {1:X}-{2:X}",path,offset,offset+length);

				java::io.OutputStream stream=session.Sftp.put(path,null,3,offset);

				int len=length;
				const int BLK=0x1000;
				while(len>BLK){
					stream.Write(buff,buffOffset,BLK);
					len-=BLK;
					buffOffset+=BLK;
				}
				stream.Write(buff,buffOffset,len);

				stream.Close();
				/*
				java::io.OutputStream stream=session.Sftp.put(path,null,3,offset);
				stream.Write(buff,buffOffset,length);
				stream.Close();
				//*/

				// 更新日時・サイズ
				{
					jsch::SftpATTRS attr=this.stat(path);

					// サイズ
					long size=attr.getSize();
					if(size<offset+length)
						attr.setSIZE(offset+length);

					// mtime
					attr.setACMODTIME(attr.getATime(),Unix.UnixTime.DateTimeToUnixTime(System.DateTime.Now));

					this.setStat(path,attr);
				}

				//((IRemoteFsOperation)this).SetMTime(path,System.DateTime.Now);
				return 0;
			}catch(System.Exception e1){
				return session.Message.ReportErr(e1);
			}
		}
		#endregion
		//==========================================================================
		//	ディスク操作
		//==========================================================================
		int DokanOperations.GetDiskFreeSpace(ref ulong freeBytesAvailable, ref ulong totalBytes, ref ulong totalFreeBytes,DokanFileInfo info){
			const long GIGA=1024*1024*1024;
			freeBytesAvailable	=10*GIGA;
			totalBytes					=20*GIGA;
			totalFreeBytes			=10*GIGA;
			return 0;
		}
		int DokanOperations.Unmount(DokanFileInfo info){
			if(this.closer!=null){
				this.closer.Dispose();
				this.closer=null;
			}
			try{
				session.Disconnect();
			}catch{}

			this.cache.Dispose();
			return 0;
		}
		//==========================================================================
		//	ディレクトリ操作
		//==========================================================================
		int InternalCreateDirectory(string filename,DokanFileInfo info){
			if(this.account.ReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
			try{
				string path=this.ResolvePath(filename);
				if(GetFileType(path)==FileType.Directory)return 0;
				session.Message.Write(1,"$ mkdir {0}",path);
				session.Sftp.mkdir(path);

				stat_cache.ClearFile(path);
				stat_cache.ClearList(Unix.UnixPath.GetDirectoryPath(path));
				return 0;
			}catch(System.Exception e1){
				return session.Message.ReportErr(e1);
			}
		}
		//--------------------------------------------------------------------------
		int InternalDeleteDirectory(string filename, DokanFileInfo info){
			if(this.account.ReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
			try{
				string path=this.ResolvePath(filename);
				session.Message.Write(1,"$ rmdir {0}",path);
				session.Sftp.rmdir(path);

				stat_cache.ClearFile(path);
				stat_cache.ClearList(Unix.UnixPath.GetDirectoryPath(path));
				return 0;
			}catch(System.Exception e1){
				return session.Message.ReportErr(e1);
			}
		}
		//--------------------------------------------------------------------------
		int InternalOpenDirectory(string filename, DokanFileInfo info){
			try{
				string path=this.ResolvePath(filename);
				if(GetFileType(path)==FileType.Directory){
					return 0;
				}
			}catch(System.Exception e1){
				int ret=session.Message.ReportErr(e1);
				if(ret==1)return 1;
			}
			return -3;
		}
		//==========================================================================
		//	Dummy File Handles
		//==========================================================================
		class FileAutoCloser:System.IDisposable{
			static System.TimeSpan TIME_CLOSEFILE=new System.TimeSpan(0,5,0);

			IDataCache cache;
			Gen::Dictionary<string,System.DateTime> times
				=new Gen::Dictionary<string,System.DateTime>();

			public FileAutoCloser(IDataCache cache){
				this.cache=cache;
				Sshfs.Program.Background+=this.bg_close;
			}
			public void Dispose(){
				Sshfs.Program.Background-=this.bg_close;
				lock(times){
					foreach(string path in times.Keys){
						try{cache.Close(path);}catch{}
					}
					times.Clear();
				}
			}
			public void Touch(string path){
				lock(times){
					if(!times.ContainsKey(path))cache.Open(path);
					times[path]=System.DateTime.Now;
				}
			}
			public bool CloseNow(string path){
				lock(times){
					if(!times.ContainsKey(path))return false;
					times.Remove(path);
				}
				cache.Close(path);
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
						cache.Close(key);
						times.Remove(key);
					}
				}
			}
		}
		//==========================================================================
		//	ファイル読み書き
		//==========================================================================
		int InternalCreateFile(string filename,FileAccess acc,FileShare share,FileMode mode, FileOptions options, DokanFileInfo info){
			try{
				if(IsAltStream(filename))return 0;
				string path=this.ResolvePath(filename);

				FileType type=GetFileType(path);
				if(type==FileType.Directory)
					info.IsDirectory=true;

				switch(mode){
					case FileMode.CreateNew:
						if(type!=FileType.NotExist)return -WinErrorCode.ERROR_ALREADY_EXISTS;
						goto put_empty;
					case FileMode.Create:
						goto put_empty;
					case FileMode.Open:
						if(type==FileType.NotExist)return -WinErrorCode.ERROR_FILE_NOT_FOUND;
						break;
					case FileMode.OpenOrCreate:
						if(type==FileType.NotExist)goto put_empty;
						break;
					case FileMode.Truncate:
						if(type==FileType.NotExist)return -WinErrorCode.ERROR_FILE_NOT_FOUND;
						goto put_empty;
					case FileMode.Append:
						if(type==FileType.NotExist)goto put_empty;
						break;
					default:
						session.Message.Write(0,"!! ERR !! unknown FileMode {0}!",mode);
						return -1;
					put_empty:
						if(this.account.ReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
						session.Sftp.put(path).Close();
						stat_cache.ClearFile(path);
						stat_cache.ClearList(Unix.UnixPath.GetDirectoryPath(path));
						break;
				}

				//this.cache.Open(path);
				/* -------------------------------------------------
				 * Dokan では open/close がちゃんと管理されていない?
				 * -------------------------------------------------
				 * CreateFile の直後に Cleanup/CloseFile が呼ばれる
				 * 更に、その後に読み出しや書込が実行されて、
				 * 終わっても CloseFile 等は呼び出されない。
				 */
				this.closer.Touch(path);

				return 0;
			}catch(System.Exception e1){
				return session.Message.ReportErr(e1);
			}
		}
		//--------------------------------------------------------------------------
		int DokanOperations.FlushFileBuffers(string filename, DokanFileInfo info){return 0;}
		int DokanOperations.Cleanup(string filename,DokanFileInfo info){return 0;}
		int DokanOperations.CloseFile(string filename,DokanFileInfo info){return 0;}
		//--------------------------------------------------------------------------
		unsafe int InternalReadFile(string filename,byte[] buffer,ref uint readBytes,long offset,DokanFileInfo info){
			if(filename.EndsWith("\\")){
				// ls などで、通常のファイル名の末端に \\ をつけて ReadFile が呼び出される…。
				return -1;
			}
			string path=this.ResolvePath(filename);
			//session.Message.Write(0,string.Format("InternalReadFile: filename={0}, path={1}",filename,path));
			if(path.Contains(":SSHFSProperty.Permission")){
				if(offset==0L){
					path=path.Split(':')[0];
					string s=this.GetPermission(path);
					fixed(byte* pbuff=buffer)fixed(char* pch=s)
						readBytes=(uint)System.Text.Encoding.ASCII.GetBytes(pch,s.Length,pbuff,buffer.Length);
					return 0;
				}
				readBytes=0;
				return 0;
			}

			FileType type=GetFileType(path);
			if(type==FileType.Directory)return -1;
			/* -----------------------------------------
			 * 何故か Directory として開いたのにも拘わらず
			 * info.IsDirectory の値が勝手に変わってしまう事がある様だ。
			 * -----------------------------------------
			 */
			//if(info.IsDirectory)return -1;

			this.closer.Touch(path);
			int length=buffer.Length;
			int ret=this.cache.ReadData(buffer,0,path,offset,ref length);
			readBytes=(uint)length;
			return ret;
		}
		private string GetPermission(string path){
			try{
				int perm=this.stat(path).getPermissions()&0xfff;
				return System.Convert.ToString(perm,8)+"\n";
			}catch(System.Exception e1){
				session.Message.ReportErr(e1);
				return "0\n";
			}
		}
		//--------------------------------------------------------------------------
		int InternalWriteFile(string filename, byte[] buffer, ref uint writtenBytes, long offset, DokanFileInfo info){
			if(this.account.ReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;

			string path=this.ResolvePath(filename);
			if(path.Contains(":SSHFSProperty.Permission")){
				if(offset==0L){
					path=path.Split(':')[0];
					int permission=0;
					permission=System.Convert.ToInt32(System.Text.Encoding.ASCII.GetString(buffer),8);
					this.SetPermission(path,permission);
					writtenBytes=(uint)buffer.Length;
					return 0;
				}
				writtenBytes=0;
				return 0;
			}

			this.closer.Touch(path);
			int length=buffer.Length;
			int ret=this.cache.WriteData(buffer,0,path,offset,ref length);
			writtenBytes=(uint)length;
			return ret;
		}
		private bool SetPermission(string path,int permission){
			try{
				jsch::SftpATTRS attr=this.stat(path);
				attr.setPERMISSIONS(permission);
				this.setStat(path,attr);
				this.stat_cache.SetAttr(path,attr);
				return true;
			}catch(System.Exception e1){
				session.Message.ReportErr(e1);
				return true;
			}
		}
		//--------------------------------------------------------------------------
		int InternalSetAllocationSize(string filename, long length,DokanFileInfo info){
			if(this.account.ReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
			try{
				string path=this.ResolvePath(filename);
				jsch::SftpATTRS attr=this.stat(path);
				if(attr.getSize()<length){
					attr.setSIZE(length);
					this.setStat(path,attr);
				}
				return 0;
			}catch(System.Exception e1){
				return session.Message.ReportErr(e1);
			}
		}
		//--------------------------------------------------------------------------
		int InternalSetEndOfFile(string filename, long length, DokanFileInfo info){
			if(this.account.ReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
			try{
				string path=this.ResolvePath(filename);
				jsch::SftpATTRS attr=this.stat(path);
				attr.setSIZE(length);
				this.setStat(path,attr);
				return 0;
			}catch(System.Exception e1){
				return session.Message.ReportErr(e1);
			}
		}
		//==========================================================================
		//	ファイル情報
		//==========================================================================
		private void CopyFileInfo(Dokan.FileInformation info,jsch::SftpATTRS r_attr){
			info.Length=r_attr.getSize();
			info.CreationTime=Unix.UnixTime.UnixTimeToDateTime(r_attr.getMTime());
			info.LastAccessTime=Unix.UnixTime.UnixTimeToDateTime(r_attr.getATime());
			info.LastWriteTime=Unix.UnixTime.UnixTimeToDateTime(r_attr.getMTime());

			// attributes
			info.Attributes=
				r_attr.isDir()?System.IO.FileAttributes.Directory:
				System.IO.FileAttributes.Normal;
			if(this.account.ReadOnly&&!r_attr.isDir())
				info.Attributes|=System.IO.FileAttributes.ReadOnly;
			if(account.Offline)
				info.Attributes|=System.IO.FileAttributes.Offline;
			if(info.FileName.Length>0&&info.FileName[0]=='.')
				info.Attributes|=System.IO.FileAttributes.Hidden;
		}
		//--------------------------------------------------------------------------
		int InternalGetFileInformation(string filename, FileInformation fileinfo, DokanFileInfo info){
			try{
				string path=this.ResolvePath(filename);
				jsch::SftpATTRS pattrs=this.stat(path);
				fileinfo.FileName=System.IO.Path.GetFileName(filename);
				this.CopyFileInfo(fileinfo,pattrs);
				return 0;
			}catch(System.Exception e1){
				return session.Message.ReportErr(e1);
			}
		}
		//--------------------------------------------------------------------------
		int InternalFindFiles(string filename,System.Collections.ArrayList files,DokanFileInfo info){
			try{
				string path=this.ResolvePath(filename);
				foreach(SftpFileInfo entry in this.ls(path)){
					FileInformation information=new FileInformation();
					information.FileName=entry.Filename;

					jsch::SftpATTRS attr=entry.Attribute;
					this.CopyFileInfo(information,attr);
					files.Add(information);
				}
				return 0;
			}catch(System.Exception e1){
				files.Clear();
				return session.Message.ReportErr(e1);
			}
		}
		//--------------------------------------------------------------------------
		int InternalSetFileAttributes(string filename, FileAttributes _attr, DokanFileInfo info){
			if(this.account.ReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
			try{
				string path=this.ResolvePath(filename);
				jsch::SftpATTRS attr=this.stat(path);
				int permissions=attr.getPermissions();
				attr.setPERMISSIONS(permissions);
				this.setStat(path,attr);
				return 0;
			}catch(System.Exception e1){
				return session.Message.ReportErr(e1);
			}
		}
		//--------------------------------------------------------------------------
		int InternalSetFileTime(string filename,System.DateTime ctime,System.DateTime atime,System.DateTime mtime,DokanFileInfo info){
			if(this.account.ReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
			try{
				string path=this.ResolvePath(filename);
				jsch::SftpATTRS attr=this.stat(path);

				// canonicalize arguments
				int asecs;
				if(atime==System.DateTime.MinValue){
					asecs=attr.getATime();
				}else{
					session.Message.Write(1,"$ touch {0} -ad {1}",path,atime);
					asecs=Unix.UnixTime.DateTimeToUnixTime(atime);
				}

				int msecs;
				if(mtime==System.DateTime.MinValue){
					msecs=attr.getMTime();
				}else{
					session.Message.Write(1,"$ touch {0} -md {1}",path,mtime);
					msecs=Unix.UnixTime.DateTimeToUnixTime(mtime);
				}

				attr.setACMODTIME(asecs,msecs);
				this.setStat(path,attr);
				return 0;
			}catch(System.Exception e1){
				return session.Message.ReportErr(e1);
			}
		}
		//==========================================================================
		//	ノード操作
		//==========================================================================
		int InternalDeleteFile(string filename, DokanFileInfo info){
			if(this.account.ReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
			try{
				string path=this.ResolvePath(filename);
				closer.CloseNow(path);
				session.Message.Write(1,"$ rm {0}",path);
				session.Sftp.rm(path);
				stat_cache.ClearFile(path);
				stat_cache.ClearList(Unix.UnixPath.GetDirectoryPath(path));
				return 0;
			}catch(System.Exception e1){
				return session.Message.ReportErr(e1);
			}
		}
		//--------------------------------------------------------------------------
		int DokanOperations.LockFile(string filename, long offset, long length, DokanFileInfo info){return 0;}
		int DokanOperations.UnlockFile(string filename,long offset,long length,DokanFileInfo info){return 0;}
		//--------------------------------------------------------------------------
		int InternalMoveFile(string filename,string newname,bool replace,DokanFileInfo info){
			System.Exception e=null;

			if(this.account.ReadOnly)return -WinErrorCode.ERROR_WRITE_PROTECT;
			try{
				string path=this.ResolvePath(filename);
				string newpath=this.ResolvePath(newname);

				// 上書き確認
				if(!replace&&this.GetFileType(newpath)!=FileType.NotExist){
					return -WinErrorCode.ERROR_ALREADY_EXISTS;
				}

				closer.CloseNow(path);
				try{
					session.Message.Write(1,"$ mv {0} {1}",path,newpath);
					session.Sftp.rename(path,newpath);
				}catch(jsch::SftpException e0){
					if(e0.message!="Failure"){
						e=e0;goto error;
					}
					string cmd=string.Format("mv {0} {1}",path,newpath);
					string hoge=session.Exec(cmd);
					session.Message.Write(5,"> result: "+hoge);
				}
				stat_cache.ClearFile(path);
				stat_cache.ClearFile(newpath);
				stat_cache.ClearList(Unix.UnixPath.GetDirectoryPath(path));
				stat_cache.ClearList(Unix.UnixPath.GetDirectoryPath(newpath));
				return 0;
			}catch(System.Exception e1){
				e=e1;goto error;
			}
		error:
			return session.Message.ReportErr(e);
		}

	}
}

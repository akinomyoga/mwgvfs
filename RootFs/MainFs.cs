using Dokan;
using Gen=System.Collections.Generic;
using mwg.Sshfs;

//using DOKAN_OPERATIONS_USED=mwg.Sshfs.SftpFsOperation;
using DOKAN_OPERATIONS_USED=mwg.mwgvfs.gvfs.GvfsOperation;

namespace mwg.Mounter{
	class FsNode{
		public Dokan.FileInformation info
			=new FileInformation();
		public FsNode(string name){
			info.FileName=name;
			info.LastWriteTime=RootFsOperation.FsUpdateTime;
			info.LastAccessTime=RootFsOperation.FsUpdateTime;
			info.CreationTime=RootFsOperation.FsUpdateTime;
		}
	}
	class FsFile:FsNode{
		public byte[] data;

		public FsFile(string name):base(name){
			this.info.Length=0;
		}
		public void SetContent(byte[] data){
			this.data=data;
			this.info.Length=this.data.Length;
		}
		public void SetContent(string content){
			this.data=System.Text.Encoding.Default.GetBytes(content);
			this.info.Length=this.data.Length;
		}

		public int Read(int offset,byte[] buffer,int buffOffset,int count){
			// count
			int rest;
			rest=this.data.Length-offset;
			if(rest<count)count=rest;
			rest=buffer.Length-buffOffset;
			if(rest<count)count=rest;
			if(count<=0)return 0;

			// copy
			System.Array.Copy(data,offset,buffer,buffOffset,count);
			System.Console.WriteLine("read {2} {0}+{1}",offset,count,this.info.FileName);
			return count;
		}
	}
	class FsDirectory:FsNode{
		public Gen::Dictionary<string,FsNode> files;

		public FsDirectory(string name):base(name){
			this.files=new Gen::Dictionary<string,FsNode>();
			this.info.Attributes|=System.IO.FileAttributes.Directory;
			this.info.Length=0;
		}

		public FsFile CreateFile(string name){
			FsFile file=new FsFile(name);
			this.files.Add(name,file);
			return file;
		}
	}
	abstract class FsMountPoint:FsDirectory{
		public FsMountPoint(string name):base(name){}
		public abstract DokanOperations FsOperation{get;}
		public abstract void Close();
	}
	class FsMountSftp:FsMountPoint{
		ISftpAccount account;
		object syncroot=new object();
		DOKAN_OPERATIONS_USED operation;

		public FsMountSftp(ISftpAccount account):base(account.Name){
			this.account=account;
		}
		public override DokanOperations FsOperation{
			get{
				lock(syncroot)if(this.operation==null){
					ISshSession session=account.CreateSession();
					session.Message.VerboseLevel=2;
					this.operation=new DOKAN_OPERATIONS_USED(session,account);
				}
				return this.operation;
			}
		}
		public override void Close(){
			lock(syncroot)if(this.operation!=null){
				((DokanOperations)this.operation).Unmount(null);
			}
		}
	}
	class RootFsOperation:DokanOperations{
		internal static System.DateTime FsUpdateTime=System.IO.File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath);
		static char[] PathSeparators=new char[]{'/','\\'};

		private FsDirectory rootdir;
		private Gen::Dictionary<string,FsMountPoint> mntlist
			=new System.Collections.Generic.Dictionary<string,FsMountPoint>();
		private FsMountPoint CheckMountPoint(ref string filename){
			foreach(string key in mntlist.Keys){
        if(filename.StartsWith(key)){
          if(filename.Length==key.Length)
            filename="";
          else if(filename[key.Length]=='\\')
            filename=filename.Substring(key.Length);
          else continue;

          return mntlist[key];
        }
			}
			return null;
		}

		FsDirectory GetDirectory(string filename){
			if(filename==null)return this.rootdir;
			string[] names=filename.ToLower().Split(PathSeparators,System.StringSplitOptions.RemoveEmptyEntries);
			FsDirectory dir=this.rootdir;
			foreach(string name in names){
				FsNode node;
				if(!dir.files.TryGetValue(name,out node))return null;
				dir=node as FsDirectory;
				if(dir==null)return null;
			}

			return dir;
		}
		FsFile GetFile(string filename){
			FsDirectory dir=GetDirectory(System.IO.Path.GetDirectoryName(filename));
			if(dir==null)return null;
			string name=System.IO.Path.GetFileName(filename).ToLower();

			FsNode node;
			if(!dir.files.TryGetValue(name,out node))return null;
			FsFile file=node as FsFile;
			return file;
#if false
			int step=0;
			try{
				step=1;
				FsDirectory dir=GetDirectory(System.IO.Path.GetDirectoryName(filename));
				step=2;
				string name=System.IO.Path.GetFileName(filename).ToLower();

				step=3;
				FsNode node;
				step=4;
				if(!dir.files.TryGetValue(name,out node))return null;
				step=5;
				FsFile file=node as FsFile;
				return file;
			}catch(System.NullReferenceException){
				System.Console.WriteLine("#DEBUG NullReferenceException at GetFile(filename={0}) : step{1}",filename,step);
				return null;
			}
#endif
		}

		//==========================================================================
		//	Initializations
		//==========================================================================
		public RootFsOperation():this(ProgramSetting.Load()){}
		public RootFsOperation(ProgramSetting setting){
			rootdir=new FsDirectory("root");
			rootdir.info.Attributes|=System.IO.FileAttributes.System;
			rootdir.info.Attributes|=System.IO.FileAttributes.ReadOnly;

			FsFile file=rootdir.CreateFile("autorun.inf");
			file.info.Attributes|=System.IO.FileAttributes.ReadOnly;
			file.info.Attributes|=System.IO.FileAttributes.Hidden;
			file.info.Attributes|=System.IO.FileAttributes.System;
			file.SetContent("[autorun]\r\nicon=drive.ico\r\n");

			file=rootdir.CreateFile("desktop.ini");
			file.info.Attributes|=System.IO.FileAttributes.ReadOnly;
			file.info.Attributes|=System.IO.FileAttributes.Hidden;
			file.info.Attributes|=System.IO.FileAttributes.System;
			file.SetContent(GetDataFromResource("desktop.ini"));

			file=rootdir.CreateFile("drive.ico");
			file.info.Attributes|=System.IO.FileAttributes.ReadOnly;
			file.info.Attributes|=System.IO.FileAttributes.Hidden;
			file.info.Attributes|=System.IO.FileAttributes.System;
			file.SetContent(GetDataFromResource("DriveIcon.ico"));

			foreach(ISftpAccount acc in setting.accounts){
        if(!acc.Enabled)continue;

				FsMountPoint mnt=new FsMountSftp(acc);
				mnt.info.Attributes|=System.IO.FileAttributes.Offline;
				rootdir.files.Add(acc.Name,mnt);
				mntlist.Add("\\"+acc.Name,mnt);
			}
		}

		static byte[] GetDataFromResource(string name){
			System.IO.Stream str=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("mwg.Sshfs.Resource."+name);
			byte[] data=new byte[str.Length];
			str.Read(data,0,(int)str.Length);
			return data;
		}
		//==========================================================================
		//	Operations
		//==========================================================================
		int DokanOperations.Cleanup(string filename,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.Cleanup(filename,info);

			return 0;
		}
		int DokanOperations.CloseFile(string filename,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.CloseFile(filename,info);

			return 0;
		}
		int DokanOperations.CreateDirectory(string filename,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.CreateDirectory(filename,info);

			return -WinErrorCode.ERROR_PATH_NOT_FOUND;
		}
		int DokanOperations.CreateFile(string filename,System.IO.FileAccess access,System.IO.FileShare share,System.IO.FileMode mode,System.IO.FileOptions options,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.CreateFile(filename,access,share,mode,options,info);

			return 0;
		}
		int DokanOperations.DeleteDirectory(string filename,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.DeleteDirectory(filename,info);

			return -WinErrorCode.ERROR_FILE_NOT_FOUND;
		}
		int DokanOperations.DeleteFile(string filename,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.DeleteFile(filename,info);

			return -WinErrorCode.ERROR_FILE_NOT_FOUND;
		}
		int DokanOperations.FindFiles(string filename,System.Collections.ArrayList files,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.FindFiles(filename,files,info);
			FsDirectory dir=GetDirectory(filename);
			if(dir==null)return -1;

			// FindFiles
			foreach(Gen::KeyValuePair<string,FsNode> node in dir.files){
				files.Add(node.Value.info);
			}
			return 0;
		}
		int DokanOperations.FlushFileBuffers(string filename,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.FlushFileBuffers(filename,info);

			return 0;
		}
		int DokanOperations.GetDiskFreeSpace(ref ulong freeBytesAvailable,ref ulong totalBytes,ref ulong totalFreeBytes,DokanFileInfo info) {
			const long GIGA=1024*1024*1024;
			freeBytesAvailable	=1024*GIGA;
			totalBytes					=2048*GIGA;
			totalFreeBytes			=1024*GIGA;
			return 0;
		}
		int DokanOperations.GetFileInformation(string filename,FileInformation fileinfo,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.GetFileInformation(filename,fileinfo,info);

			FsFile file=GetFile(filename);
			if(file==null)return -1;
			fileinfo.FileName=file.info.FileName;
			fileinfo.Length=file.info.Length;
			fileinfo.Attributes=file.info.Attributes;
			fileinfo.CreationTime=file.info.CreationTime;
			fileinfo.LastAccessTime=file.info.LastAccessTime;
			fileinfo.LastWriteTime=file.info.LastWriteTime;
			return 0;
		}
		int DokanOperations.LockFile(string filename,long offset,long length,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.LockFile(filename,offset,length,info);

			return 0;
		}
    int DokanOperations.MoveFile(string filename,string newname,bool replace,DokanFileInfo info){
      FsMountPoint mnt1=CheckMountPoint(ref filename);
      FsMountPoint mnt2=CheckMountPoint(ref newname);
      if(mnt1==null)
        return -WinErrorCode.ERROR_FILE_NOT_FOUND;
      if(mnt2==null)
        return -WinErrorCode.ERROR_PATH_NOT_FOUND;

      if(mnt1==mnt2){
        return mnt1.FsOperation.MoveFile(filename,newname,replace,info);
      }else{
        return -WinErrorCode.ERROR_NOT_SAME_DEVICE;
      }
    }
		int DokanOperations.OpenDirectory(string filename,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.OpenDirectory(filename,info);

			FsDirectory dir=GetDirectory(filename);
			if(dir==null)return -WinErrorCode.ERROR_FILE_NOT_FOUND;
			return 0;
		}
		int DokanOperations.ReadFile(string filename,byte[] buffer,ref uint readBytes,long offset,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.ReadFile(filename,buffer,ref readBytes,offset,info);

			readBytes=0;
			FsFile file=GetFile(filename);
			if(file==null)
        return -WinErrorCode.ERROR_FILE_NOT_FOUND;

			int r=file.Read((int)offset,buffer,0,buffer.Length);
			if(r<=0)return -1;
			readBytes=(uint)r;
			return 0;
		}
		int DokanOperations.SetAllocationSize(string filename,long length,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.SetAllocationSize(filename,length,info);

			return -WinErrorCode.ERROR_FILE_NOT_FOUND;
		}
		int DokanOperations.SetEndOfFile(string filename,long length,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.SetEndOfFile(filename,length,info);

			return -WinErrorCode.ERROR_FILE_NOT_FOUND;
		}
		int DokanOperations.SetFileAttributes(string filename,System.IO.FileAttributes attr,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.SetFileAttributes(filename,attr,info);

			return -WinErrorCode.ERROR_FILE_NOT_FOUND;
		}
		int DokanOperations.SetFileTime(string filename,System.DateTime ctime,System.DateTime atime,System.DateTime mtime,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.SetFileTime(filename,ctime,atime,mtime,info);

			return -WinErrorCode.ERROR_FILE_NOT_FOUND;
		}
		int DokanOperations.UnlockFile(string filename,long offset,long length,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.UnlockFile(filename,offset,length,info);

			return 0;
		}
		int DokanOperations.Unmount(DokanFileInfo info){
			foreach(FsMountPoint mnt in mntlist.Values){
				mnt.Close();
			}

			return 0;
		}
		int DokanOperations.WriteFile(string filename,byte[] buffer,ref uint writtenBytes,long offset,DokanFileInfo info){
			FsMountPoint mnt=CheckMountPoint(ref filename);
			if(mnt!=null)return mnt.FsOperation.WriteFile(filename,buffer,ref writtenBytes,offset,info);

			return -WinErrorCode.ERROR_PATH_NOT_FOUND;
		}
	}
}
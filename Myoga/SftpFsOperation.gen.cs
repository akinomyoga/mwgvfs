#line 1 "SftpFsOperation.tmpl.inl"
using Dokan;
using System.IO;
using jsch=Tamir.SharpSsh.jsch;
using java=Tamir.SharpSsh.java;
using Gen=System.Collections.Generic;

namespace mwg.Sshfs{
	partial class SftpFsOperation:DokanOperations{
		int DokanOperations.CreateDirectory(string filename,DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalCreateDirectory(filename,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalCreateDirectory(filename,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 33 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.DeleteDirectory(string filename, DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalDeleteDirectory(filename,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalDeleteDirectory(filename,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 36 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.OpenDirectory(string filename, DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalOpenDirectory(filename,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalOpenDirectory(filename,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 39 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.CreateFile(string filename,FileAccess acc,FileShare share,FileMode mode, FileOptions options, DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalCreateFile(filename,acc,share,mode,options,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalCreateFile(filename,acc,share,mode,options,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 42 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.ReadFile(string filename,byte[] buffer,ref uint readBytes,long offset,DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalReadFile(filename,buffer,ref readBytes,offset,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalReadFile(filename,buffer,ref readBytes,offset,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 45 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.WriteFile(string filename, byte[] buffer, ref uint writtenBytes, long offset, DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalWriteFile(filename,buffer,ref writtenBytes,offset,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalWriteFile(filename,buffer,ref writtenBytes,offset,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 48 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.SetAllocationSize(string filename, long length,DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalSetAllocationSize(filename,length,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalSetAllocationSize(filename,length,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 51 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.SetEndOfFile(string filename, long length, DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalSetEndOfFile(filename,length,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalSetEndOfFile(filename,length,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 54 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.GetFileInformation(string filename, FileInformation fileinfo, DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalGetFileInformation(filename,fileinfo,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalGetFileInformation(filename,fileinfo,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 57 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.FindFiles(string filename,System.Collections.ArrayList files,DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalFindFiles(filename,files,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalFindFiles(filename,files,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 60 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.SetFileAttributes(string filename, FileAttributes _attr, DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalSetFileAttributes(filename,_attr,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalSetFileAttributes(filename,_attr,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 63 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.SetFileTime(string filename,System.DateTime ctime,System.DateTime atime,System.DateTime mtime,DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalSetFileTime(filename,ctime,atime,mtime,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalSetFileTime(filename,ctime,atime,mtime,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 66 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.DeleteFile(string filename, DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalDeleteFile(filename,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalDeleteFile(filename,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 69 "SftpFsOperation.tmpl.inl"
		}
		int DokanOperations.MoveFile(string filename,string newname,bool replace,DokanFileInfo info){
#line 21 "SftpFsOperation.tmpl.inl"
			int ret=InternalMoveFile(filename,newname,replace,info);
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalMoveFile(filename,newname,replace,info);
				if(ret!=1)return ret;
			}
			return -1;
#line 72 "SftpFsOperation.tmpl.inl"
		}
	}
}

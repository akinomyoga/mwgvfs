using System.IO;
using jsch=Tamir.SharpSsh.jsch;
using java=Tamir.SharpSsh.java;
using Gen=System.Collections.Generic;

namespace mwg.Sshfs{
	partial class SftpFsOperation:DokanOperations{
		int DokanOperations.CreateDirectory(string filename,DokanFileInfo info){
#mwg:define 1
			int c=this.account.ReconnectCount;
			while(true){
				int ret=InternalFunctionCall;
				if(ret!=1)return ret;
				if(--c<0)return -1;
				this.session.Reconnect();
			}
#mwg:define end
#mwg:define 2
			int ret=InternalFunctionCall;
			if(ret!=1)return ret;

			int c=this.account.ReconnectCount;
			while(c--!=0){
				if(!this.session.Reconnect())continue;
				ret=InternalFunctionCall;
				if(ret!=1)return ret;
			}
			return -1;
#mwg:define end
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalCreateDirectory(filename,info)/
		}
		int DokanOperations.DeleteDirectory(string filename, DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalDeleteDirectory(filename,info)/
		}
		int DokanOperations.OpenDirectory(string filename, DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalOpenDirectory(filename,info)/
		}
		int DokanOperations.CreateFile(string filename,FileAccess acc,FileShare share,FileMode mode, FileOptions options, DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalCreateFile(filename,acc,share,mode,options,info)/
		}
		int DokanOperations.ReadFile(string filename,byte[] buffer,ref uint readBytes,long offset,DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalReadFile(filename,buffer,ref readBytes,offset,info)/
		}
		int DokanOperations.WriteFile(string filename, byte[] buffer, ref uint writtenBytes, long offset, DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalWriteFile(filename,buffer,ref writtenBytes,offset,info)/
		}
		int DokanOperations.SetAllocationSize(string filename, long length,DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalSetAllocationSize(filename,length,info)/
		}
		int DokanOperations.SetEndOfFile(string filename, long length, DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalSetEndOfFile(filename,length,info)/
		}
		int DokanOperations.GetFileInformation(string filename, FileInformation fileinfo, DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalGetFileInformation(filename,fileinfo,info)/
		}
		int DokanOperations.FindFiles(string filename,System.Collections.ArrayList files,DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalFindFiles(filename,files,info)/
		}
		int DokanOperations.SetFileAttributes(string filename, FileAttributes _attr, DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalSetFileAttributes(filename,_attr,info)/
		}
		int DokanOperations.SetFileTime(string filename,System.DateTime ctime,System.DateTime atime,System.DateTime mtime,DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalSetFileTime(filename,ctime,atime,mtime,info)/
		}
		int DokanOperations.DeleteFile(string filename, DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalDeleteFile(filename,info)/
		}
		int DokanOperations.MoveFile(string filename,string newname,bool replace,DokanFileInfo info){
#mwg:expand 2.r/\bInternalFunctionCall\b/InternalMoveFile(filename,newname,replace,info)/
		}
	}
}

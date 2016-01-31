namespace mwg.Mounter{
	using Dokan;
	using Gen=System.Collections.Generic;
	using System.IO;

	class DebugFsOperation:Dokan.DokanOperations{
		Dokan.DokanOperations ops;
		public DebugFsOperation(Dokan.DokanOperations ops){
			this.ops=ops;
		}

		int DokanOperations.CreateFile(string filename,FileAccess access,FileShare share,FileMode mode,FileOptions options,DokanFileInfo info) {
			System.Console.WriteLine("debug: CreateFile {0}",filename);
			return ops.CreateFile(filename,access,share,mode,options,info);
		}
		int DokanOperations.Cleanup(string filename,DokanFileInfo info){
			System.Console.WriteLine("debug: Cleanup {0}",filename);
			return ops.Cleanup(filename,info);
		}
		int DokanOperations.CloseFile(string filename,DokanFileInfo info){
			System.Console.WriteLine("debug: CloseFile {0}",filename);
			return ops.CloseFile(filename,info);
		}
		int DokanOperations.FlushFileBuffers(string filename,DokanFileInfo info) {
			System.Console.WriteLine("debug: FlushFileBuffers {0}",filename);
			return ops.FlushFileBuffers(filename,info);
		}
		int DokanOperations.ReadFile(string filename,byte[] buffer,ref uint mbuffer,long offset,DokanFileInfo info){
			System.Console.WriteLine("debug: ReadFile {0}",filename);
			return ops.ReadFile(filename,buffer,ref mbuffer,offset,info);
		}
		int DokanOperations.WriteFile(string filename,byte[] buffer,ref uint writtenBytes,long offset,DokanFileInfo info) {
			System.Console.WriteLine("debug: WriteFile {0}",filename);
			return ops.WriteFile(filename,buffer,ref writtenBytes,offset,info);
		}

		#region DokanOperations メンバ
		int DokanOperations.CreateDirectory(string filename,DokanFileInfo info) {
			System.Console.WriteLine("debug: CreateDirectory {0}",filename);
			return ops.CreateDirectory(filename,info);
		}
		int DokanOperations.DeleteDirectory(string filename,DokanFileInfo info) {
			System.Console.WriteLine("debug: DeleteDirectory {0}",filename);
			return ops.DeleteDirectory(filename,info);
		}
		int DokanOperations.DeleteFile(string filename,DokanFileInfo info) {
			System.Console.WriteLine("debug: DeleteFile {0}",filename);
			return ops.DeleteFile(filename,info);
		}
		int DokanOperations.FindFiles(string filename,System.Collections.ArrayList files,DokanFileInfo info) {
			System.Console.WriteLine("debug: FindFiles {0}",filename);
			return ops.FindFiles(filename,files,info);
		}
		int DokanOperations.GetDiskFreeSpace(ref ulong freeBytesAvailable,ref ulong totalBytes,ref ulong totalFreeBytes,DokanFileInfo info) {
			return ops.GetDiskFreeSpace(ref freeBytesAvailable,ref totalBytes,ref totalFreeBytes,info);
		}
		int DokanOperations.GetFileInformation(string filename,FileInformation fileinfo,DokanFileInfo info) {
			System.Console.WriteLine("debug: GetFileInformation {0}",filename);
			return ops.GetFileInformation(filename,fileinfo,info);
		}
		int DokanOperations.LockFile(string filename,long offset,long length,DokanFileInfo info) {
			System.Console.WriteLine("debug: LockFile {0}",filename);
			return ops.LockFile(filename,offset,length,info);
		}
		int DokanOperations.MoveFile(string filename,string newname,bool replace,DokanFileInfo info) {
			System.Console.WriteLine("debug: MoveFile {0}",filename);
			return ops.MoveFile(filename,newname,replace,info);
		}
		int DokanOperations.OpenDirectory(string filename,DokanFileInfo info) {
			System.Console.WriteLine("debug: OpenDirectory {0}",filename);
			return ops.OpenDirectory(filename,info);
		}
		int DokanOperations.SetAllocationSize(string filename,long length,DokanFileInfo info) {
			System.Console.WriteLine("debug: SetAllocationSize {0}",filename);
			return ops.SetAllocationSize(filename,length,info);
		}
		int DokanOperations.SetEndOfFile(string filename,long length,DokanFileInfo info) {
			System.Console.WriteLine("debug: SetEndOfFile {0}",filename);
			return ops.SetEndOfFile(filename,length,info);
		}
		int DokanOperations.SetFileAttributes(string filename,FileAttributes attr,DokanFileInfo info) {
			System.Console.WriteLine("debug: SetFileAttributes {0}",filename);
			return ops.SetFileAttributes(filename,attr,info);
		}
		int DokanOperations.SetFileTime(string filename,System.DateTime ctime,System.DateTime atime,System.DateTime mtime,DokanFileInfo info) {
			System.Console.WriteLine("debug: SetFileTime {0}",filename);
			return ops.SetFileTime(filename,ctime,atime,mtime,info);
		}
		int DokanOperations.UnlockFile(string filename,long offset,long length,DokanFileInfo info) {
			System.Console.WriteLine("debug: UnlockFile {0}",filename);
			return ops.UnlockFile(filename,offset,length,info);
		}
		int DokanOperations.Unmount(DokanFileInfo info) {
			return ops.Unmount(info);
		}
		#endregion
	}

}
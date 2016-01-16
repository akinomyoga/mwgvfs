using Gen=System.Collections.Generic;
using System.Text;
using Interop=System.Runtime.InteropServices;
using Dokan;
using jsch=Tamir.SharpSsh.jsch;
using java=Tamir.SharpSsh.java;

namespace mwg.Sshfs{
	public interface IRemoteFsOperation:Dokan.DokanOperations{
		string ResolvePath(string filepath);

		// For RW Cache
		void SetMTime(string path,System.DateTime mtime);
		System.DateTime FileGetMTime(string path);
		long FileGetSize(string path);
		int ReadData(byte[] buff,int buffOffset,string path,long offset,ref int length);
		int WriteData(byte[] buff,int buffOffset,string path,long offset,ref int length);
	}

}

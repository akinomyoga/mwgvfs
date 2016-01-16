namespace mwg.Sshfs{
	using jsch=Tamir.SharpSsh.jsch;
	using java=Tamir.SharpSsh.java;

	class SftpFileInfo{
		private SftpFsOperation operation;
		private string filepath;
		private jsch::SftpATTRS attr;
		internal System.DateTime infodate;

		public string Filepath{
			get{return this.filepath;}
		}
		public string Filename{
			get{return Unix.UnixPath.GetFileName(filepath);}
		}
		public jsch::SftpATTRS Attribute{
			get{
				if(operation.SymlinkTreatment==SftpSymlink.Dereference){
					lock(attr)if(attr.isLink())
						operation.ResolveLink(filepath,ref attr,30);
				}
				
				return this.attr;
			}
		}

		public System.DateTime MTime{
			get{return Unix.UnixTime.UnixTimeToDateTime(this.Attribute.getMTime());}
		}

		//==========================================================================
		//	初期化子
		//==========================================================================
		public SftpFileInfo(SftpFsOperation operation,string filepath,jsch::SftpATTRS attr)
			:this(operation,filepath,attr,System.DateTime.Now){}

		public SftpFileInfo(
			SftpFsOperation operation,string filepath,
			jsch::SftpATTRS attr,System.DateTime infodate
		){
			this.operation=operation;
			this.filepath=filepath;
			this.attr=attr;
			this.infodate=infodate;
		}
	}

}
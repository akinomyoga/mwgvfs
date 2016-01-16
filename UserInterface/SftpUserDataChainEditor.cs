using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace mwg.Sshfs.UserInterface {
	public partial class SftpUserDataChainEditor:SftpUserDataChainEditorBase {
		public SftpUserDataChainEditor() {
			InitializeComponent();
		}
		protected override void SetToEditor(int index){
			this.sftpLoginInfoEditor1.UserData=index<0?null:this.List[index];
		}
	}

	public class SftpUserDataChainEditorBase:afh.Collections.CollectionEditorBase<SshUserData>{
		protected override SshUserData CreateNewInstance() {
			return new SshUserData();
		}
		protected override void SetToEditor(int index){
			throw new NotImplementedException();
		}
	}
}

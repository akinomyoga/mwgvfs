using System;
using System.Collections.Generic;
using System.ComponentModel;
using Gdi=System.Drawing;
using System.Data;
using System.Text;
using Forms=System.Windows.Forms;

namespace mwg.Sshfs.UserInterface {
	public partial class SftpLoginInfoEditor:Forms::UserControl {
		public SftpLoginInfoEditor() {
			InitializeComponent();
		}

		SshUserData data;
		public SshUserData UserData{
			get{return this.data;}
			set{
				if(this.data==value)return;
				this.data=value;
				if(this.data!=null){
					this.txtUser.Text=this.data.FullName;
					this.txtIdtt.Text=this.data.idtt;
					this.chkUseId.Checked=this.data.useIdentityFile;
					this.UpdateState_UseIdentityFile(this.data.useIdentityFile);
				}
			}
		}

		//------------------------------------------------------------------------
		// Event Handlers
		private void txtUser_TextChanged(object sender,System.EventArgs e){
			if(this.data==null)return;
			string line=this.txtUser.Text.Trim();
			if(this.data.FullName==line||this.data.TrySetFullName(line)){
				this.txtUser.ForeColor=Gdi::SystemColors.WindowText;
				this.txtUser.BackColor=Gdi::SystemColors.Window;
				this.txtUser.Text=this.data.FullName;
			}else{
				this.txtUser.BackColor=Gdi::Color.LavenderBlush;
				this.txtUser.ForeColor=Gdi::Color.Red;
			}
		}
		private void txtPass_TextChanged(object sender,System.EventArgs e){
			if(this.data==null)return;
			if(this.data.useIdentityFile)
				this.data.psph=this.txtPass.Text;
			else
				this.data.pass=this.txtPass.Text;
		}
		private void txtIdtt_TextChanged(object sender,System.EventArgs e) {
			if(this.data==null)return;
			this.data.idtt=this.txtIdtt.Text;
			this.txtIdtt_UpdateState();
		}
		private void btnIdtt_Click(object sender,System.EventArgs e) {
			if(this.data==null)return;
			this.diagIdtt.FileName=this.data.idtt;
			Forms::DialogResult result=this.diagIdtt.ShowDialog(this.ParentForm);
			if(result==Forms::DialogResult.OK){
				this.txtIdtt.Text=this.diagIdtt.FileName;
				this.data.idtt=this.diagIdtt.FileName;
			}
		}
		private void chkUseId_CheckedChanged(object sender,System.EventArgs e){
			if(this.data==null)return;
			this.data.useIdentityFile=this.chkUseId.Checked;
			this.UpdateState_UseIdentityFile(this.chkUseId.Checked);
			this.txtIdtt_UpdateState();
		}

		//------------------------------------------------------------------------
		// Update Control States

		//private bool UseIdentityFile{
		//  get{return this.data.useIdentityFile;}
		//  set{
		//    if(this.data.useIdentityFile==value)return;
		//    this.data.useIdentityFile=value;
		//    this.UpdateState_UseIdentityFile(value);
		//    this.txtIdtt_UpdateState();
		//  }
		//}

		private void UpdateState_UseIdentityFile(bool value){
			if(value){
				this.label3.Text="パスフレーズ(&P)";
				this.txtPass.Text=this.data.psph;
				this.btnIdtt.Enabled=true;
			}else{
				this.label3.Text="パスワード(&P)";
				this.txtPass.Text=this.data.pass;
				this.btnIdtt.Enabled=false;
			}
		}
		private void txtIdtt_UpdateState(){
			if(this.data.useIdentityFile){
				this.txtIdtt.Enabled=true;
				if(System.IO.File.Exists(this.data.idtt)){
					this.txtIdtt.ForeColor=Gdi::SystemColors.WindowText;
					this.txtIdtt.BackColor=Gdi::SystemColors.Window;
				}else if(this.data.useIdentityFile){
					this.txtIdtt.BackColor=Gdi::Color.LavenderBlush;
					this.txtIdtt.ForeColor=Gdi::Color.Red;
				}
			}else{
				this.txtIdtt.Enabled=false;
				this.txtIdtt.BackColor=Gdi::Color.Empty;
				this.txtIdtt.ForeColor=Gdi::Color.Empty;
			}
		}

		private void SftpLoginInfoEditor_SizeChanged(object sender,EventArgs e) {
			int width=this.Size.Width-2;
			this.btnIdtt.Left=width-this.btnIdtt.Width;
			this.txtIdtt.Width=this.btnIdtt.Left-6-this.txtIdtt.Left;
			this.txtPass.Width=width-this.txtPass.Left;
			this.txtUser.Width=width-this.txtUser.Left;
		}

	}
}

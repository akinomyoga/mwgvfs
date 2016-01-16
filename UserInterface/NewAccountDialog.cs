using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using Forms=System.Windows.Forms;

//using IAccount=mwg.Mounter.IFsAccount;
using IAccount=mwg.Sshfs.ISftpAccount;

namespace mwg.Sshfs.UserInterface {
	public partial class NewAccountDialog:Forms::Form {
		public NewAccountDialog() {
			InitializeComponent();
		}

		public IAccount CreateAccount(){
			if(this.accType1.Checked){
				return new SftpAccount("<new>");
			}else{
				return new SftpAccountGw("<new>");
			}
		}

		private void btnOK_Click(object sender,EventArgs e){
			this.DialogResult=Forms::DialogResult.OK;
			this.Close();
		}

		private void btnCancel_Click(object sender,EventArgs e) {
			this.DialogResult=Forms::DialogResult.Cancel;
			this.Close();
		}
	}
}

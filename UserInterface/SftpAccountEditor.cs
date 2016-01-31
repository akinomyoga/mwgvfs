using Gen=System.Collections.Generic;
using CM=System.ComponentModel;
using Forms=System.Windows.Forms;
using Gdi=System.Drawing;

namespace mwg.Sshfs.UserInterface{
	internal partial class SftpAccountEditor:Forms::UserControl,mwg.Mounter.IAccountEditor{
		SftpAccount account;
		public SftpAccount Account{
			get{return this.account;}
			set{
				if(this.account==value)return;
				this.account=value;
				if(this.account!=null){
					this.txtName.Text=this.account.Name;
					this.edUserData.UserData=this.account.data;
					this.sftpCommonSetting.AccountData=this.account;
				}
			}
		}

		public SftpAccountEditor(){
			InitializeComponent();
		}
		private void txtName_TextChanged(object sender,System.EventArgs e){
			account.Name=this.txtName.Text;
		}

		#region IAccountEditor メンバ
		public bool TrySetAccount(mwg.Mounter.IFsAccount acc){
			this.Account=acc as SftpAccount;
			return this.account!=null;
		}
		#endregion



	}
}

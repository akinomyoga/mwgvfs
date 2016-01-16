using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using Forms=System.Windows.Forms;

using Gen=System.Collections.Generic;
using mwg.Mounter;

namespace mwg.Sshfs.UserInterface{
	public partial class WndSetting:Forms::Form{
		ProgramSetting setting=ProgramSetting.Load();
		mwg.Mounter.IFsAccount account=null;
		Mounter m;

		public WndSetting(){
			//afh.Collections.CollectionEditorBase;
			InitializeComponent();
			this.btnAccNew.Image=afh.Drawing.Icons.AddNew;
			this.btnAccUp.Image=afh.Drawing.Icons.SortUp;
			this.btnAccDn.Image=afh.Drawing.Icons.SortDown;
			this.btnAccDel.Image=afh.Drawing.Icons.Delete2;
		}

		private void listBox1_SelectedIndexChanged(object sender,System.EventArgs e) {
			ListBoxItem item=this.listBox1.SelectedItem as ListBoxItem;
			if(item==null)return;
			SetAccount(item.Account);
		}
	
		private void btnSaveCfg_Click(object sender,System.EventArgs e) {
			ProgramSetting.Save(setting);
			this.listUpdate();
		}
		//==========================================================================
		//	アカウント一覧操作
		//==========================================================================
		private NewAccountDialog newAccountDialog=null;
		private void btnNewAcc_Click(object sender,System.EventArgs e) {
			if(newAccountDialog==null)newAccountDialog=new NewAccountDialog();
			if(this.newAccountDialog.ShowDialog(this)!=System.Windows.Forms.DialogResult.OK)return;

			this.setting.AddAccount(this.newAccountDialog.CreateAccount());
			this.listUpdate();
		}
		private void btnAccUp_Click(object sender,System.EventArgs e) {
			int i=this.listBox1.SelectedIndex;
			if(AccExchange(i,i-1))
				this.listBox1.SelectedIndex=i-1;
		}
		private void btnAccDn_Click(object sender,System.EventArgs e) {
			int i=this.listBox1.SelectedIndex;
			if(AccExchange(i,i+1))
				this.listBox1.SelectedIndex=i+1;
		}
		private void btnAccDel_Click(object sender,System.EventArgs e) {
			int i=this.listBox1.SelectedIndex;
			if(i<0||i>=this.listBox1.Items.Count)return;
			this.setting.accounts.RemoveAt(i);
			this.listBox1.Items.RemoveAt(i);

			// 選択項目の再設定
			if(this.listBox1.Items.Count==i)i--;
			if(i>=0)this.listBox1.SelectedIndex=i;
		}
		private bool AccExchange(int i,int j){
			if(i<0||j<0||i>=setting.accounts.Count||j>=setting.accounts.Count)
				return false;
			lock(setting.accounts){
				IFsAccount tmp=setting.accounts[i];
				setting.accounts[i]=setting.accounts[j];
				setting.accounts[j]=tmp;
			}
			lock(listBox1.Items){
				object tmp=this.listBox1.Items[i];
				this.listBox1.Items[i]=this.listBox1.Items[j];
				this.listBox1.Items[j]=tmp;
			}
			return true;
		}

		class ListBoxItem{
			private IFsAccount acc;
			public ListBoxItem(IFsAccount acc){
				this.acc=acc;
			}
			public IFsAccount Account{
				get{return this.acc;}
			}
			public override string ToString(){
				return this.acc.Name;
			}
		}

		void listUpdate(){
			this.listBox1.Items.Clear();
			foreach(mwg.Mounter.IFsAccount acc in setting.accounts){
				this.listBox1.Items.Add(new ListBoxItem(acc));
			}
		}
		//==========================================================================
		//	各アカウントの編集
		//==========================================================================
		private IAccountEditor currentEditor;
		public IAccountEditor CurrentEditor{
			get{return this.currentEditor;}
			internal set{
				if(this.currentEditor==value)return;
				if(this.currentEditor!=null){
					((Forms::Control)this.currentEditor).Dock=Forms::DockStyle.None;
					((Forms::Control)this.currentEditor).Visible=false;
				}
				this.currentEditor=value;
				if(this.currentEditor!=null){
					((Forms::Control)this.currentEditor).Dock=Forms::DockStyle.Fill;
					((Forms::Control)this.currentEditor).Visible=true;
				}
			}
		}
		private Gen::List<IAccountEditor> editors=new Gen::List<IAccountEditor>();
		void SetAccount(IFsAccount acc){
			this.account=acc;
			foreach(IAccountEditor ed in editors){
				if(!ed.TrySetAccount(acc))continue;
				this.CurrentEditor=ed;
				return;
			}

			// 新しいエディタインスタンス
			IAccountEditor newEditor=acc.CreateEditorInstance();
			Forms::Control ctrl=newEditor as Forms::Control;
			if(ctrl==null)
				throw new System.Exception("IAccountEditor を実装するクラスは System.Windows.Forms.Control を継承しなければなりません。");
			if(!newEditor.TrySetAccount(acc))
				throw new System.Exception("IAccount.CreateEditorInstance によって得られたエディタは、TrySetAccount で元のアカウントを受理する必要があります。");

			// 登録
			this.editors.Add(newEditor);
			this.editorContainer.Controls.Add(ctrl);
			this.CurrentEditor=newEditor;
		}
		//==========================================================================
		//	マウント・アンマウント操作
		//==========================================================================
		private void SshfsSetting_Load(object sender,System.EventArgs e){
			this.listUpdate();
			if(this.listBox1.Items.Count>0)
				this.listBox1.SelectedItem=this.listBox1.Items[0];
		}

		private void btnConnect_Click(object sender,System.EventArgs e) {
			if(account==null)return;
			this.mniMount.Visible=false;
			this.Hide();
			this.Mount();
			this.mniUnmount.Visible=true;
		}

		void Mount(){

			// mount
			m=new Mounter();
			/*
			// connect
			ISshSession session=account.CreateSession();
			session.Message.VerboseLevel=5;
			SftpFsOperation sshfs=new SftpFsOperation(session,account);
			m.operation=sshfs;
			//m.operation=new DebugFsOperation(sshfs);
			//*/
			m.operation           =new mwg.Mounter.RootFsOperation(this.setting);
			m.option.DebugMode    =false;//mwg.Sshfs.Program.DokanDebug;
			m.option.UseAltStream =true;
#if DOKAN060
			m.option.MountPoint   =Program.arguments.DriveLetter+@":\";
#else 
			m.option.DriveLetter  =Program.arguments.DriveLetter;
#endif
			m.option.ThreadCount  =0;
			m.option.UseKeepAlive =true;
			m.option.VolumeLabel  ="mnt";
			m.Mount();

#if DOKAN060
			this.notifyIcon1.BalloonTipText="Mounting on "+m.option.MountPoint+".";
#else
			this.notifyIcon1.BalloonTipText="Mounting on "+m.option.DriveLetter+":\\.";
#endif
		}
		
		private void btnCancel_Click(object sender,System.EventArgs e) {
			this.Hide();
		}
		private void btnExit_Click(object sender,System.EventArgs e){
			this.CmdExit();
		}
		private void mniUnmount_Click(object sender,System.EventArgs e){
			this.mniUnmount.Visible=false;
			this.m.Unmount();
			this.mniMount.Visible=true;
			this.notifyIcon1.BalloonTipText="Not mouting.";
		}
		private void mniExit_Click(object sender,System.EventArgs e) {
			this.CmdExit();
		}
		private void mniMount_Click(object sender,System.EventArgs e) {
			this.Show();
		}

		void CmdExit(){
			if(this.m!=null)this.m.UnmountWait();
			Forms::Application.Exit();
		}

		class Mounter:IDisposable{
			public Dokan.DokanOptions    option=new Dokan.DokanOptions();
			public Dokan.DokanOperations operation;
			private System.Threading.Thread worker;

			bool ismounting=false;
			public Mounter(){
			}

			public void Mount(){
				if(ismounting)return;
				ismounting=true;

				/*
				if(this.disableCache.Checked){
					worker=new MountWorker(this.fsoperation, this.opt);
				}else{
					worker=new MountWorker(new CacheOperations(this.fsoperation),this.opt);
				}
				//*/
				this.worker=new System.Threading.Thread(this.work);
#if DOKAN060
				this.worker.Name=@"<mwg::Sshfs> "+this.option.MountPoint+@" Mounter";
#else
				this.worker.Name=@"<mwg::Sshfs> "+this.option.DriveLetter+@":\ Mounter";
#endif
				this.worker.Start();
			}
			public void Unmount(){
				if(!ismounting)return;
				this.ismounting=false;

#if DOKAN060
				Dokan.DokanNet.DokanUnmount(option.MountPoint[0]);
#else
				Dokan.DokanNet.DokanUnmount(option.DriveLetter);
#endif
				this.operation.Unmount(null);
			}
			public void UnmountWait(){
				this.Unmount();
				if(this.worker.IsAlive)this.worker.Join();
			}
			public void Dispose(){
				if(!ismounting)return;
				this.Unmount();
			}
			void work(){
				//Directory.SetCurrentDirectory(Application.StartupPath);
				string text=null;
				try{
					int rcode=Dokan.DokanNet.DokanMain(this.option,this.operation);
					if(rcode<0)switch(rcode){
						case -5:text="Dokan drive letter assign error";break;
						case -4:text="Dokan driver error, please reboot";break;
						case -3:text="Dokan driver install error";break;
						case -2:text="Dokan drive letter error";break;
						case -1:text="Dokan Error";break;
						default:text="Dokan Error";break;
					}
				}catch(System.Exception e){
					text=e.ToString();
				}

				// termination
				if(text!=null){
					Forms::MessageBox.Show(text, "Fatal Error");
					Forms::Application.Exit();
				}
			}
		}
	}
}

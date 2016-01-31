namespace mwg.Sshfs.UserInterface{
	partial class WndSetting{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing){
			if(disposing && (components != null)){
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WndSetting));
			this.listBox1 = new System.Windows.Forms.ListBox();
			this.btnConnect = new System.Windows.Forms.Button();
			this.btnCancel = new System.Windows.Forms.Button();
			this.notifyMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.mniExit = new System.Windows.Forms.ToolStripMenuItem();
			this.mniUnmount = new System.Windows.Forms.ToolStripMenuItem();
			this.mniMount = new System.Windows.Forms.ToolStripMenuItem();
			this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
			this.btnExit = new System.Windows.Forms.Button();
			this.btnAccNew = new System.Windows.Forms.Button();
			this.btnSaveCfg = new System.Windows.Forms.Button();
			this.btnAccUp = new System.Windows.Forms.Button();
			this.btnAccDn = new System.Windows.Forms.Button();
			this.btnAccDel = new System.Windows.Forms.Button();
			this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
			this.editorContainer = new System.Windows.Forms.ContainerControl();
			this.notifyMenu.SuspendLayout();
			this.SuspendLayout();
			// 
			// listBox1
			// 
			this.listBox1.FormattingEnabled = true;
			this.listBox1.ItemHeight = 12;
			this.listBox1.Location = new System.Drawing.Point(44,12);
			this.listBox1.Name = "listBox1";
			this.listBox1.Size = new System.Drawing.Size(145,424);
			this.listBox1.TabIndex = 4;
			this.listBox1.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
			// 
			// btnConnect
			// 
			this.btnConnect.Location = new System.Drawing.Point(396,446);
			this.btnConnect.Name = "btnConnect";
			this.btnConnect.Size = new System.Drawing.Size(86,23);
			this.btnConnect.TabIndex = 7;
			this.btnConnect.Text = "マウント(&M)";
			this.btnConnect.UseVisualStyleBackColor = true;
			this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
			// 
			// btnCancel
			// 
			this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnCancel.Location = new System.Drawing.Point(488,446);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(86,23);
			this.btnCancel.TabIndex = 8;
			this.btnCancel.Text = "キャンセル(&C)";
			this.btnCancel.UseVisualStyleBackColor = true;
			this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
			// 
			// notifyMenu
			// 
			this.notifyMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mniExit,
            this.mniUnmount,
            this.mniMount});
			this.notifyMenu.Name = "Exit";
			this.notifyMenu.ShowImageMargin = false;
			this.notifyMenu.Size = new System.Drawing.Size(115,70);
			// 
			// mniExit
			// 
			this.mniExit.Name = "mniExit";
			this.mniExit.Size = new System.Drawing.Size(114,22);
			this.mniExit.Text = "終了(&X)";
			this.mniExit.Click += new System.EventHandler(this.mniExit_Click);
			// 
			// mniUnmount
			// 
			this.mniUnmount.Name = "mniUnmount";
			this.mniUnmount.Size = new System.Drawing.Size(114,22);
			this.mniUnmount.Text = "アンマウント(&U)";
			this.mniUnmount.Visible = false;
			this.mniUnmount.Click += new System.EventHandler(this.mniUnmount_Click);
			// 
			// mniMount
			// 
			this.mniMount.Name = "mniMount";
			this.mniMount.Size = new System.Drawing.Size(114,22);
			this.mniMount.Text = "マウント...(&M)";
			this.mniMount.Click += new System.EventHandler(this.mniMount_Click);
			// 
			// notifyIcon1
			// 
			this.notifyIcon1.ContextMenuStrip = this.notifyMenu;
			this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
			this.notifyIcon1.Text = "茗荷 Sshfs";
			this.notifyIcon1.Visible = true;
			// 
			// btnExit
			// 
			this.btnExit.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnExit.Location = new System.Drawing.Point(580,446);
			this.btnExit.Name = "btnExit";
			this.btnExit.Size = new System.Drawing.Size(86,23);
			this.btnExit.TabIndex = 9;
			this.btnExit.Text = "終了(&X)";
			this.btnExit.UseVisualStyleBackColor = true;
			this.btnExit.Click += new System.EventHandler(this.btnExit_Click);
			// 
			// btnAccNew
			// 
			this.btnAccNew.Location = new System.Drawing.Point(12,12);
			this.btnAccNew.Name = "btnAccNew";
			this.btnAccNew.Size = new System.Drawing.Size(26,23);
			this.btnAccNew.TabIndex = 0;
			this.toolTip1.SetToolTip(this.btnAccNew,"アカウント新規作成");
			this.btnAccNew.UseVisualStyleBackColor = true;
			this.btnAccNew.Click += new System.EventHandler(this.btnNewAcc_Click);
			// 
			// btnSaveCfg
			// 
			this.btnSaveCfg.Location = new System.Drawing.Point(304,446);
			this.btnSaveCfg.Name = "btnSaveCfg";
			this.btnSaveCfg.Size = new System.Drawing.Size(86,23);
			this.btnSaveCfg.TabIndex = 6;
			this.btnSaveCfg.Text = "設定保存(&S)";
			this.btnSaveCfg.UseVisualStyleBackColor = true;
			this.btnSaveCfg.Click += new System.EventHandler(this.btnSaveCfg_Click);
			// 
			// btnAccUp
			// 
			this.btnAccUp.Location = new System.Drawing.Point(12,41);
			this.btnAccUp.Name = "btnAccUp";
			this.btnAccUp.Size = new System.Drawing.Size(26,23);
			this.btnAccUp.TabIndex = 1;
			this.toolTip1.SetToolTip(this.btnAccUp,"上に移動");
			this.btnAccUp.UseVisualStyleBackColor = true;
			this.btnAccUp.Click += new System.EventHandler(this.btnAccUp_Click);
			// 
			// btnAccDn
			// 
			this.btnAccDn.Location = new System.Drawing.Point(12,70);
			this.btnAccDn.Name = "btnAccDn";
			this.btnAccDn.Size = new System.Drawing.Size(26,23);
			this.btnAccDn.TabIndex = 2;
			this.toolTip1.SetToolTip(this.btnAccDn,"下に移動");
			this.btnAccDn.UseVisualStyleBackColor = true;
			this.btnAccDn.Click += new System.EventHandler(this.btnAccDn_Click);
			// 
			// btnAccDel
			// 
			this.btnAccDel.Location = new System.Drawing.Point(12,99);
			this.btnAccDel.Name = "btnAccDel";
			this.btnAccDel.Size = new System.Drawing.Size(26,23);
			this.btnAccDel.TabIndex = 3;
			this.toolTip1.SetToolTip(this.btnAccDel,"削除");
			this.btnAccDel.UseVisualStyleBackColor = true;
			this.btnAccDel.Click += new System.EventHandler(this.btnAccDel_Click);
			// 
			// editorContainer
			// 
			this.editorContainer.Location = new System.Drawing.Point(195,12);
			this.editorContainer.Name = "editorContainer";
			this.editorContainer.Size = new System.Drawing.Size(471,428);
			this.editorContainer.TabIndex = 5;
			// 
			// SshfsSetting
			// 
			this.AcceptButton = this.btnConnect;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F,12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.btnCancel;
			this.ClientSize = new System.Drawing.Size(678,481);
			this.Controls.Add(this.editorContainer);
			this.Controls.Add(this.btnAccDel);
			this.Controls.Add(this.btnAccDn);
			this.Controls.Add(this.btnAccUp);
			this.Controls.Add(this.listBox1);
			this.Controls.Add(this.btnAccNew);
			this.Controls.Add(this.btnExit);
			this.Controls.Add(this.btnSaveCfg);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.btnConnect);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "SshfsSetting";
			this.Text = "SshfsSetting";
			this.Load += new System.EventHandler(this.SshfsSetting_Load);
			this.notifyMenu.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ListBox listBox1;
		private System.Windows.Forms.Button btnConnect;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.ContextMenuStrip notifyMenu;
		private System.Windows.Forms.ToolStripMenuItem mniExit;
		private System.Windows.Forms.ToolStripMenuItem mniUnmount;
		private System.Windows.Forms.ToolStripMenuItem mniMount;
		private System.Windows.Forms.NotifyIcon notifyIcon1;
		private System.Windows.Forms.Button btnExit;
		private System.Windows.Forms.Button btnAccNew;
		private System.Windows.Forms.Button btnSaveCfg;
		private System.Windows.Forms.Button btnAccUp;
		private System.Windows.Forms.Button btnAccDn;
		private System.Windows.Forms.Button btnAccDel;
		private System.Windows.Forms.ToolTip toolTip1;
		private System.Windows.Forms.ContainerControl editorContainer;
	}
}
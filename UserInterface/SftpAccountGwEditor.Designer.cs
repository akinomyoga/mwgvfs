namespace mwg.Sshfs.UserInterface {
	partial class SftpAccountGwEditor {
		/// <summary> 
		/// 必要なデザイナ変数です。
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// 使用中のリソースをすべてクリーンアップします。
		/// </summary>
		/// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region コンポーネント デザイナで生成されたコード

		/// <summary> 
		/// デザイナ サポートに必要なメソッドです。このメソッドの内容を 
		/// コード エディタで変更しないでください。
		/// </summary>
		private void InitializeComponent() {
      this.label1 = new System.Windows.Forms.Label();
      this.txtName = new System.Windows.Forms.TextBox();
      this.label2 = new System.Windows.Forms.Label();
      this.sftpCommonSetting = new mwg.Sshfs.UserInterface.SftpAccountCommonEditor();
      this.edLoginChain = new mwg.Sshfs.UserInterface.SftpUserDataChainEditor();
      this.SuspendLayout();
      // 
      // label1
      // 
      this.label1.AutoSize = true;
      this.label1.Location = new System.Drawing.Point(3,10);
      this.label1.Name = "label1";
      this.label1.Size = new System.Drawing.Size(77,12);
      this.label1.TabIndex = 0;
      this.label1.Text = "アカウント名(&N)";
      // 
      // txtName
      // 
      this.txtName.Location = new System.Drawing.Point(88,7);
      this.txtName.Name = "txtName";
      this.txtName.Size = new System.Drawing.Size(373,19);
      this.txtName.TabIndex = 1;
      this.txtName.TextChanged += new System.EventHandler(this.txtName_TextChanged);
      // 
      // label2
      // 
      this.label2.AutoSize = true;
      this.label2.Location = new System.Drawing.Point(3,38);
      this.label2.Name = "label2";
      this.label2.Size = new System.Drawing.Size(102,12);
      this.label2.TabIndex = 2;
      this.label2.Text = "接続経路の設定(&P)";
      // 
      // sftpCommonSetting
      // 
      this.sftpCommonSetting.AccountData = null;
      this.sftpCommonSetting.Location = new System.Drawing.Point(0,198);
      this.sftpCommonSetting.Name = "sftpCommonSetting";
      this.sftpCommonSetting.Size = new System.Drawing.Size(461,188);
      this.sftpCommonSetting.TabIndex = 4;
      // 
      // edLoginChain
      // 
      this.edLoginChain.Location = new System.Drawing.Point(5,53);
      this.edLoginChain.Name = "edLoginChain";
      this.edLoginChain.Padding = new System.Windows.Forms.Padding(1);
      this.edLoginChain.Size = new System.Drawing.Size(456,141);
      this.edLoginChain.TabIndex = 5;
      // 
      // SftpAccountGwEditor
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F,12F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.Controls.Add(this.edLoginChain);
      this.Controls.Add(this.label2);
      this.Controls.Add(this.label1);
      this.Controls.Add(this.txtName);
      this.Controls.Add(this.sftpCommonSetting);
      this.Name = "SftpAccountGwEditor";
      this.Size = new System.Drawing.Size(623,402);
      this.ResumeLayout(false);
      this.PerformLayout();

		}

		#endregion

		private SftpAccountCommonEditor sftpCommonSetting;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox txtName;
		private System.Windows.Forms.Label label2;
		private mwg.Sshfs.UserInterface.SftpUserDataChainEditor edLoginChain;
	}
}

namespace mwg.Sshfs.UserInterface
{
	partial class SftpAccountEditor{
		/// <summary> 
		/// 必要なデザイナ変数です。
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// 使用中のリソースをすべてクリーンアップします。
		/// </summary>
		/// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region コンポーネント デザイナで生成されたコード

		/// <summary> 
		/// デザイナ サポートに必要なメソッドです。このメソッドの内容を 
		/// コード エディタで変更しないでください。
		/// </summary>
		private void InitializeComponent()
		{
      this.txtName = new System.Windows.Forms.TextBox();
      this.label1 = new System.Windows.Forms.Label();
      this.edUserData = new mwg.Sshfs.UserInterface.SftpLoginInfoEditor();
      this.sftpCommonSetting = new mwg.Sshfs.UserInterface.SftpAccountCommonEditor();
      this.SuspendLayout();
      // 
      // txtName
      // 
      this.txtName.Location = new System.Drawing.Point(106,8);
      this.txtName.Name = "txtName";
      this.txtName.Size = new System.Drawing.Size(365,19);
      this.txtName.TabIndex = 1;
      this.txtName.TextChanged += new System.EventHandler(this.txtName_TextChanged);
      // 
      // label1
      // 
      this.label1.AutoSize = true;
      this.label1.Location = new System.Drawing.Point(3,11);
      this.label1.Name = "label1";
      this.label1.Size = new System.Drawing.Size(77,12);
      this.label1.TabIndex = 0;
      this.label1.Text = "アカウント名(&N)";
      // 
      // edUserData
      // 
      this.edUserData.Location = new System.Drawing.Point(3,33);
      this.edUserData.Name = "edUserData";
      this.edUserData.Size = new System.Drawing.Size(468,91);
      this.edUserData.TabIndex = 11;
      this.edUserData.UserData = null;
      // 
      // sftpCommonSetting
      // 
      this.sftpCommonSetting.AccountData = null;
      this.sftpCommonSetting.Location = new System.Drawing.Point(3,130);
      this.sftpCommonSetting.Name = "sftpCommonSetting";
      this.sftpCommonSetting.Size = new System.Drawing.Size(495,197);
      this.sftpCommonSetting.TabIndex = 10;
      // 
      // SftpAccountEditor
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F,12F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.Controls.Add(this.edUserData);
      this.Controls.Add(this.sftpCommonSetting);
      this.Controls.Add(this.label1);
      this.Controls.Add(this.txtName);
      this.Name = "SftpAccountEditor";
      this.Size = new System.Drawing.Size(517,330);
      this.ResumeLayout(false);
      this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox txtName;
		private System.Windows.Forms.Label label1;
		private mwg.Sshfs.UserInterface.SftpAccountCommonEditor sftpCommonSetting;
		private SftpLoginInfoEditor edUserData;
	}
}

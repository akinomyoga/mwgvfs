namespace mwg.Sshfs.UserInterface {
	partial class SftpLoginInfoEditor {
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
			this.btnIdtt = new System.Windows.Forms.Button();
			this.chkUseId = new System.Windows.Forms.CheckBox();
			this.label4 = new System.Windows.Forms.Label();
			this.txtIdtt = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.txtPass = new System.Windows.Forms.TextBox();
			this.txtUser = new System.Windows.Forms.TextBox();
			this.diagIdtt = new System.Windows.Forms.OpenFileDialog();
			this.SuspendLayout();
			// 
			// btnIdtt
			// 
			this.btnIdtt.Location = new System.Drawing.Point(407,72);
			this.btnIdtt.Name = "btnIdtt";
			this.btnIdtt.Size = new System.Drawing.Size(61,19);
			this.btnIdtt.TabIndex = 7;
			this.btnIdtt.Text = "参照(&B)...";
			this.btnIdtt.UseVisualStyleBackColor = true;
			this.btnIdtt.Click += new System.EventHandler(this.btnIdtt_Click);
			// 
			// chkUseId
			// 
			this.chkUseId.AutoSize = true;
			this.chkUseId.Location = new System.Drawing.Point(0,25);
			this.chkUseId.Name = "chkUseId";
			this.chkUseId.Size = new System.Drawing.Size(127,16);
			this.chkUseId.TabIndex = 2;
			this.chkUseId.Text = "鍵認証を使用する(&K)";
			this.chkUseId.UseVisualStyleBackColor = true;
			this.chkUseId.CheckedChanged += new System.EventHandler(this.chkUseId_CheckedChanged);
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(3,75);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(86,12);
			this.label4.TabIndex = 5;
			this.label4.Text = "秘密鍵ファイル(&I)";
			// 
			// txtIdtt
			// 
			this.txtIdtt.Location = new System.Drawing.Point(103,72);
			this.txtIdtt.Name = "txtIdtt";
			this.txtIdtt.Size = new System.Drawing.Size(298,19);
			this.txtIdtt.TabIndex = 6;
			this.txtIdtt.TextChanged += new System.EventHandler(this.txtIdtt_TextChanged);
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(3,50);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(67,12);
			this.label3.TabIndex = 3;
			this.label3.Text = "パスワード(&P)";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(0,3);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(94,12);
			this.label2.TabIndex = 0;
			this.label2.Text = "user@host:port(&F)";
			// 
			// txtPass
			// 
			this.txtPass.Location = new System.Drawing.Point(103,47);
			this.txtPass.Name = "txtPass";
			this.txtPass.PasswordChar = '*';
			this.txtPass.Size = new System.Drawing.Size(365,19);
			this.txtPass.TabIndex = 4;
			this.txtPass.TextChanged += new System.EventHandler(this.txtPass_TextChanged);
			// 
			// txtUser
			// 
			this.txtUser.Location = new System.Drawing.Point(103,0);
			this.txtUser.Name = "txtUser";
			this.txtUser.Size = new System.Drawing.Size(365,19);
			this.txtUser.TabIndex = 1;
			this.txtUser.TextChanged += new System.EventHandler(this.txtUser_TextChanged);
			// 
			// diagIdtt
			// 
			this.diagIdtt.FileName = "openFileDialog1";
			// 
			// SftpLoginInfoEditor
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F,12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.btnIdtt);
			this.Controls.Add(this.chkUseId);
			this.Controls.Add(this.label4);
			this.Controls.Add(this.txtIdtt);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.txtPass);
			this.Controls.Add(this.txtUser);
			this.Name = "SftpLoginInfoEditor";
			this.Size = new System.Drawing.Size(481,113);
			this.SizeChanged += new System.EventHandler(this.SftpLoginInfoEditor_SizeChanged);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Button btnIdtt;
		private System.Windows.Forms.CheckBox chkUseId;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.TextBox txtIdtt;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.TextBox txtPass;
		private System.Windows.Forms.TextBox txtUser;
		private System.Windows.Forms.OpenFileDialog diagIdtt;
	}
}

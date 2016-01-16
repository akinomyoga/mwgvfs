namespace mwg.Sshfs.UserInterface {
	partial class SftpUserDataChainEditor {
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

		#region Windows フォーム デザイナで生成されたコード

		/// <summary>
		/// デザイナ サポートに必要なメソッドです。このメソッドの内容を
		/// コード エディタで変更しないでください。
		/// </summary>
		private void InitializeComponent() {
			this.sftpLoginInfoEditor1 = new mwg.Sshfs.UserInterface.SftpLoginInfoEditor();
			this.SuspendLayout();
			// 
			// panel1
			// 
			this.panel1.Dock = System.Windows.Forms.DockStyle.Left;
			this.panel1.Location = new System.Drawing.Point(1,1);
			this.panel1.Size = new System.Drawing.Size(28,173);
			// 
			// listBox1
			// 
			this.listBox1.Dock = System.Windows.Forms.DockStyle.Left;
			this.listBox1.Location = new System.Drawing.Point(29,1);
			this.listBox1.Size = new System.Drawing.Size(109,172);
			// 
			// sftpLoginInfoEditor1
			// 
			this.sftpLoginInfoEditor1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.sftpLoginInfoEditor1.Location = new System.Drawing.Point(138,1);
			this.sftpLoginInfoEditor1.Name = "sftpLoginInfoEditor1";
			this.sftpLoginInfoEditor1.Padding = new System.Windows.Forms.Padding(3,0,0,0);
			this.sftpLoginInfoEditor1.Size = new System.Drawing.Size(313,173);
			this.sftpLoginInfoEditor1.TabIndex = 6;
			this.sftpLoginInfoEditor1.UserData = null;
			// 
			// SftpUserDataChainEditor
			// 
			this.Controls.Add(this.sftpLoginInfoEditor1);
			this.Name = "SftpUserDataChainEditor";
			this.Size = new System.Drawing.Size(452,175);
			this.Controls.SetChildIndex(this.panel1,0);
			this.Controls.SetChildIndex(this.listBox1,0);
			this.Controls.SetChildIndex(this.sftpLoginInfoEditor1,0);
			this.ResumeLayout(false);

		}

		#endregion

		private SftpLoginInfoEditor sftpLoginInfoEditor1;
	}
}

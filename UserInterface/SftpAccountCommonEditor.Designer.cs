namespace mwg.Sshfs.UserInterface {
	partial class SftpAccountCommonEditor {
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
      this.label10 = new System.Windows.Forms.Label();
      this.label11 = new System.Windows.Forms.Label();
      this.numHeartbeat = new System.Windows.Forms.NumericUpDown();
      this.label9 = new System.Windows.Forms.Label();
      this.label8 = new System.Windows.Forms.Label();
      this.label7 = new System.Windows.Forms.Label();
      this.numDisconnectInt = new System.Windows.Forms.NumericUpDown();
      this.label6 = new System.Windows.Forms.Label();
      this.label5 = new System.Windows.Forms.Label();
      this.chkReadonly = new System.Windows.Forms.CheckBox();
      this.cmbSymlink = new System.Windows.Forms.ComboBox();
      this.numReconnectCount = new System.Windows.Forms.NumericUpDown();
      this.chkOffline = new System.Windows.Forms.CheckBox();
      this.label4 = new System.Windows.Forms.Label();
      this.txtRootDir = new System.Windows.Forms.TextBox();
      this.chkEnabled = new System.Windows.Forms.CheckBox();
      ((System.ComponentModel.ISupportInitialize)(this.numHeartbeat)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.numDisconnectInt)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.numReconnectCount)).BeginInit();
      this.SuspendLayout();
      // 
      // label10
      // 
      this.label10.AutoSize = true;
      this.label10.Location = new System.Drawing.Point(208,143);
      this.label10.Name = "label10";
      this.label10.Size = new System.Drawing.Size(77,12);
      this.label10.TabIndex = 13;
      this.label10.Text = "秒以上経過時";
      // 
      // label11
      // 
      this.label11.AutoSize = true;
      this.label11.Location = new System.Drawing.Point(-1,143);
      this.label11.Name = "label11";
      this.label11.Size = new System.Drawing.Size(99,12);
      this.label11.TabIndex = 11;
      this.label11.Text = "Heartbeat 間隔(&H)";
      // 
      // numHeartbeat
      // 
      this.numHeartbeat.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
      this.numHeartbeat.Location = new System.Drawing.Point(132,141);
      this.numHeartbeat.Maximum = new decimal(new int[] {
            3600,
            0,
            0,
            0});
      this.numHeartbeat.Name = "numHeartbeat";
      this.numHeartbeat.Size = new System.Drawing.Size(70,19);
      this.numHeartbeat.TabIndex = 12;
      this.numHeartbeat.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      this.numHeartbeat.Value = new decimal(new int[] {
            60,
            0,
            0,
            0});
      this.numHeartbeat.ValueChanged += new System.EventHandler(this.numHeartbeat_ValueChanged);
      // 
      // label9
      // 
      this.label9.AutoSize = true;
      this.label9.Location = new System.Drawing.Point(208,93);
      this.label9.Name = "label9";
      this.label9.Size = new System.Drawing.Size(17,12);
      this.label9.TabIndex = 7;
      this.label9.Text = "回";
      // 
      // label8
      // 
      this.label8.AutoSize = true;
      this.label8.Location = new System.Drawing.Point(208,118);
      this.label8.Name = "label8";
      this.label8.Size = new System.Drawing.Size(77,12);
      this.label8.TabIndex = 10;
      this.label8.Text = "秒以上経過時";
      // 
      // label7
      // 
      this.label7.AutoSize = true;
      this.label7.Location = new System.Drawing.Point(-1,118);
      this.label7.Name = "label7";
      this.label7.Size = new System.Drawing.Size(93,12);
      this.label7.TabIndex = 8;
      this.label7.Text = "自動切断間隔(&A)";
      // 
      // numDisconnectInt
      // 
      this.numDisconnectInt.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
      this.numDisconnectInt.Location = new System.Drawing.Point(132,116);
      this.numDisconnectInt.Maximum = new decimal(new int[] {
            86400,
            0,
            0,
            0});
      this.numDisconnectInt.Name = "numDisconnectInt";
      this.numDisconnectInt.Size = new System.Drawing.Size(70,19);
      this.numDisconnectInt.TabIndex = 9;
      this.numDisconnectInt.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      this.numDisconnectInt.Value = new decimal(new int[] {
            300,
            0,
            0,
            0});
      this.numDisconnectInt.ValueChanged += new System.EventHandler(this.numDisconnectInt_ValueChanged);
      // 
      // label6
      // 
      this.label6.AutoSize = true;
      this.label6.Location = new System.Drawing.Point(-2,169);
      this.label6.Name = "label6";
      this.label6.Size = new System.Drawing.Size(128,12);
      this.label6.TabIndex = 14;
      this.label6.Text = "シンボリックリンクの表示(&L)";
      // 
      // label5
      // 
      this.label5.AutoSize = true;
      this.label5.Location = new System.Drawing.Point(-1,93);
      this.label5.Name = "label5";
      this.label5.Size = new System.Drawing.Size(105,12);
      this.label5.TabIndex = 5;
      this.label5.Text = "再接続試行回数(&R)";
      // 
      // chkReadonly
      // 
      this.chkReadonly.AutoSize = true;
      this.chkReadonly.Location = new System.Drawing.Point(1,69);
      this.chkReadonly.Name = "chkReadonly";
      this.chkReadonly.Size = new System.Drawing.Size(272,16);
      this.chkReadonly.TabIndex = 4;
      this.chkReadonly.Text = "書込プロテクト(&W) 読取専用の領域として使用します";
      this.chkReadonly.UseVisualStyleBackColor = true;
      this.chkReadonly.CheckedChanged += new System.EventHandler(this.chkReadonly_CheckedChanged);
      // 
      // cmbSymlink
      // 
      this.cmbSymlink.FormattingEnabled = true;
      this.cmbSymlink.Items.AddRange(new object[] {
            "リンク先ファイルに解決",
            "通常ファイルとして直接表示"});
      this.cmbSymlink.Location = new System.Drawing.Point(132,166);
      this.cmbSymlink.Name = "cmbSymlink";
      this.cmbSymlink.Size = new System.Drawing.Size(182,20);
      this.cmbSymlink.TabIndex = 15;
      this.cmbSymlink.Text = "リンク先ファイルに解決";
      this.cmbSymlink.SelectedIndexChanged += new System.EventHandler(this.cmbSymlink_SelectedIndexChanged);
      // 
      // numReconnectCount
      // 
      this.numReconnectCount.Location = new System.Drawing.Point(132,91);
      this.numReconnectCount.Maximum = new decimal(new int[] {
            5,
            0,
            0,
            0});
      this.numReconnectCount.Name = "numReconnectCount";
      this.numReconnectCount.Size = new System.Drawing.Size(70,19);
      this.numReconnectCount.TabIndex = 6;
      this.numReconnectCount.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      this.numReconnectCount.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
      this.numReconnectCount.ValueChanged += new System.EventHandler(this.numReconnectCount_ValueChanged);
      // 
      // chkOffline
      // 
      this.chkOffline.AutoSize = true;
      this.chkOffline.Location = new System.Drawing.Point(1,47);
      this.chkOffline.Name = "chkOffline";
      this.chkOffline.Size = new System.Drawing.Size(133,16);
      this.chkOffline.TabIndex = 3;
      this.chkOffline.Text = "オフラインとして表示(&O)";
      this.chkOffline.UseVisualStyleBackColor = true;
      this.chkOffline.CheckedChanged += new System.EventHandler(this.chkOffline_CheckedChanged);
      // 
      // label4
      // 
      this.label4.AutoSize = true;
      this.label4.Location = new System.Drawing.Point(-1,25);
      this.label4.Name = "label4";
      this.label4.Size = new System.Drawing.Size(98,12);
      this.label4.TabIndex = 1;
      this.label4.Text = "ルートディレクトリ(&D)";
      // 
      // txtRootDir
      // 
      this.txtRootDir.Location = new System.Drawing.Point(103,22);
      this.txtRootDir.Name = "txtRootDir";
      this.txtRootDir.Size = new System.Drawing.Size(354,19);
      this.txtRootDir.TabIndex = 2;
      this.txtRootDir.TextChanged += new System.EventHandler(this.txtRootDir_TextChanged);
      // 
      // chkEnabled
      // 
      this.chkEnabled.AutoSize = true;
      this.chkEnabled.Location = new System.Drawing.Point(0,0);
      this.chkEnabled.Name = "chkEnabled";
      this.chkEnabled.Size = new System.Drawing.Size(172,16);
      this.chkEnabled.TabIndex = 0;
      this.chkEnabled.Text = "ファイルシステムを有効にする(&E)";
      this.chkEnabled.UseVisualStyleBackColor = true;
      this.chkEnabled.CheckedChanged += new System.EventHandler(this.chkEnabled_CheckedChanged);
      // 
      // SftpAccountCommonEditor
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F,12F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.Controls.Add(this.chkEnabled);
      this.Controls.Add(this.label4);
      this.Controls.Add(this.txtRootDir);
      this.Controls.Add(this.label10);
      this.Controls.Add(this.label11);
      this.Controls.Add(this.numHeartbeat);
      this.Controls.Add(this.label9);
      this.Controls.Add(this.label8);
      this.Controls.Add(this.label7);
      this.Controls.Add(this.numDisconnectInt);
      this.Controls.Add(this.label6);
      this.Controls.Add(this.label5);
      this.Controls.Add(this.chkReadonly);
      this.Controls.Add(this.cmbSymlink);
      this.Controls.Add(this.numReconnectCount);
      this.Controls.Add(this.chkOffline);
      this.Name = "SftpAccountCommonEditor";
      this.Size = new System.Drawing.Size(468,195);
      ((System.ComponentModel.ISupportInitialize)(this.numHeartbeat)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.numDisconnectInt)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.numReconnectCount)).EndInit();
      this.ResumeLayout(false);
      this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label label10;
		private System.Windows.Forms.Label label11;
		private System.Windows.Forms.NumericUpDown numHeartbeat;
		private System.Windows.Forms.Label label9;
		private System.Windows.Forms.Label label8;
		private System.Windows.Forms.Label label7;
		private System.Windows.Forms.NumericUpDown numDisconnectInt;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.CheckBox chkReadonly;
		private System.Windows.Forms.ComboBox cmbSymlink;
		private System.Windows.Forms.NumericUpDown numReconnectCount;
		private System.Windows.Forms.CheckBox chkOffline;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.TextBox txtRootDir;
    private System.Windows.Forms.CheckBox chkEnabled;
	}
}

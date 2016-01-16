namespace mwg.Sshfs.UserInterface {
	partial class NewAccountDialog {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.btnOK = new System.Windows.Forms.Button();
			this.btnCancel = new System.Windows.Forms.Button();
			this.accType1 = new System.Windows.Forms.RadioButton();
			this.accType2 = new System.Windows.Forms.RadioButton();
			this.SuspendLayout();
			// 
			// btnOK
			// 
			this.btnOK.Location = new System.Drawing.Point(104,60);
			this.btnOK.Name = "btnOK";
			this.btnOK.Size = new System.Drawing.Size(85,23);
			this.btnOK.TabIndex = 0;
			this.btnOK.Text = "&OK";
			this.btnOK.UseVisualStyleBackColor = true;
			this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
			// 
			// btnCancel
			// 
			this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnCancel.Location = new System.Drawing.Point(195,60);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(85,23);
			this.btnCancel.TabIndex = 1;
			this.btnCancel.Text = "キャンセル(&C)";
			this.btnCancel.UseVisualStyleBackColor = true;
			this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
			// 
			// accType1
			// 
			this.accType1.AutoSize = true;
			this.accType1.Checked = true;
			this.accType1.Location = new System.Drawing.Point(12,12);
			this.accType1.Name = "accType1";
			this.accType1.Size = new System.Drawing.Size(99,16);
			this.accType1.TabIndex = 2;
			this.accType1.TabStop = true;
			this.accType1.Text = "SFTP アカウント";
			this.accType1.UseVisualStyleBackColor = true;
			// 
			// accType2
			// 
			this.accType2.AutoSize = true;
			this.accType2.Location = new System.Drawing.Point(12,34);
			this.accType2.Name = "accType2";
			this.accType2.Size = new System.Drawing.Size(183,16);
			this.accType2.TabIndex = 3;
			this.accType2.Text = "SFTP アカウント (Gateway 対応)";
			this.accType2.UseVisualStyleBackColor = true;
			// 
			// NewAccountDialog
			// 
			this.AcceptButton = this.btnOK;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F,12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.btnCancel;
			this.ClientSize = new System.Drawing.Size(292,95);
			this.Controls.Add(this.accType2);
			this.Controls.Add(this.accType1);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.btnOK);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "NewAccountDialog";
			this.Text = "新しいアカウントの追加";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Button btnOK;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.RadioButton accType1;
		private System.Windows.Forms.RadioButton accType2;
	}
}
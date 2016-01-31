using Gen=System.Collections.Generic;
using CM=System.ComponentModel;
using Gdi=System.Drawing;
using Forms=System.Windows.Forms;

namespace mwg.Sshfs.UserInterface {
	internal partial class SftpAccountGwEditor:Forms::UserControl,mwg.Mounter.IAccountEditor{
		public SftpAccountGwEditor() {
			InitializeComponent();
		}

		private SftpAccountGw account;
		public SftpAccountGw Account{
			get{return this.account;}
			set{
				if(this.account==value)return;
				this.account=value;
				if(this.account!=null){
					this.txtName.Text=account.Name;
					this.sftpCommonSetting.AccountData=this.account;
					this.edLoginChain.List=this.account.gwchain;
				}
			}
		}
		private void txtName_TextChanged(object sender,System.EventArgs e){
			account.Name=this.txtName.Text;
		}

		#region IAccountEditor メンバ
		public bool TrySetAccount(mwg.Mounter.IFsAccount acc){
			this.Account=acc as SftpAccountGw;
			return this.account!=null;
		}
		#endregion

#if false
		//OOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOO
		//	セルの描画
		//--------------------------------------------------------------------------
		void dataGridView1_CellPainting(object sender,Forms::DataGridViewCellPaintingEventArgs e){
			if(e.RowIndex<0)return;

			CellDrawer drawer=new CellDrawer(this.gridGwchain,e);
			if(e.ColumnIndex==0){
				if(e.Value==null){
					drawer.text=e.CellStyle.NullValue.ToString();
				}else{
					drawer.text=(string)e.Value;
					if(drawer.data!=null&&!drawer.data.FullnameForEditIsValid){
						drawer.text+=" <形式が間違っています>";
						drawer.isvalid=false;
					}
				}

				drawer.DetermineColor();
				drawer.Draw();
				e.Handled=true;
			}else if(e.ColumnIndex==1){
				if(e.Value==null||(string)e.Value==""){
					drawer.text=e.CellStyle.NullValue.ToString();
				}else{
					gridGwchain.Rows[e.RowIndex].Cells[1].ToolTipText="<パスワード>";
					drawer.text=new string('*',((string)e.Value).Length);
				}
				drawer.DetermineColor();
				drawer.Draw();
				e.Handled=true;
			}
		}
		private struct CellDrawer{
			readonly Forms::DataGridViewCellPaintingEventArgs e;
			readonly Forms::DataGridView sender;
			public readonly SshUserData data;

			public bool selected;
			public bool isvalid;
			Gdi::Color cBack;
			Gdi::Color cFore;

			public string text;

			public CellDrawer(Forms::DataGridView sender,Forms::DataGridViewCellPaintingEventArgs e){
				this.sender=sender;
				this.e=e;
				this.data=this.sender.Rows[e.RowIndex].DataBoundItem as SshUserData;

				this.selected=0!=(e.State&Forms::DataGridViewElementStates.Selected);
				this.isvalid=true;
				this.cBack=default(Gdi::Color);
				this.cFore=default(Gdi::Color);
				this.text=null;
			}

			public void DetermineColor(){
				if(selected){
					if(isvalid){
						cBack=e.CellStyle.SelectionBackColor;
						cFore=e.CellStyle.SelectionForeColor;
					}else{
						cBack=Gdi::Color.Red;
						cFore=Gdi::Color.White;
					}
				}else{
					if(data==null){
						cBack=Gdi::Color.Silver;
						cFore=Gdi::Color.Black;
					}else if(isvalid){
						cBack=e.CellStyle.BackColor;
						cFore=e.CellStyle.ForeColor;
					}else{
						cBack=Gdi::Color.LavenderBlush;
						cFore=Gdi::Color.Red;
					}
				}
			}

			public void Draw(){
				using(Gdi::Pen penGrid=new Gdi::Pen(sender.GridColor))
				using(Gdi::Brush brBack=new Gdi::SolidBrush(cBack))
				using(Gdi::Brush brFore=new Gdi::SolidBrush(cFore)){
					// Clear
					e.Graphics.FillRectangle(brBack,e.CellBounds);
					e.Graphics.DrawLine(penGrid,
						e.CellBounds.Left,e.CellBounds.Bottom-1,
						e.CellBounds.Right-1,e.CellBounds.Bottom-1);
					e.Graphics.DrawLine(penGrid,
						e.CellBounds.Right-1,e.CellBounds.Top,
						e.CellBounds.Right-1,e.CellBounds.Bottom);

					int h=(e.CellBounds.Height-e.CellStyle.Font.Height)/2;
					e.Graphics.DrawString(
						text,
						e.CellStyle.Font,brFore,
						e.CellBounds.X+1,e.CellBounds.Y+h,
						Gdi::StringFormat.GenericDefault
						);
				}
			}
		}
#endif
	}
}

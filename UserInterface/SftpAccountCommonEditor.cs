using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace mwg.Sshfs.UserInterface{
  internal interface ISftpAccountCommonSetting{
    string ServerRoot{get;set;}            // ルートにするサーバ側ディレクトリ
    bool Offline{get;set;}                 // オフライン表示
    bool ReadOnly{get;set;}                // 読み取り専用
    int ReconnectCount{get;set;}           // 再接続試行回数
    int DisconnectInterval{get;set;}       // 負値: 常時接続
    int HeartbeatInterval{get;set;}        // 負値: Heartbeat を送らない
    SftpSymlink SymlinkTreatment{get;set;}
    bool Enabled{get;set;}
  }

  internal partial class SftpAccountCommonEditor:UserControl{
    public SftpAccountCommonEditor(){
      InitializeComponent();
    }

    ISftpAccountCommonSetting accountData=null;
    public ISftpAccountCommonSetting AccountData{
      get{return this.accountData;}
      set{
        if(this.accountData==value)return;
        this.accountData=value;
        if(this.accountData!=null){
          this.chkEnabled.Checked=accountData.Enabled;
          this.txtRootDir.Text=accountData.ServerRoot;
          this.chkOffline.Checked=accountData.Offline;
          this.chkReadonly.Checked=accountData.ReadOnly;
          this.numReconnectCount.Value=accountData.ReconnectCount;
          this.numDisconnectInt.Value=accountData.DisconnectInterval;
          this.numHeartbeat.Value=accountData.HeartbeatInterval;
          switch(accountData.SymlinkTreatment){
            case SftpSymlink.Dereference:
            default:
              this.cmbSymlink.SelectedIndex=0;
              break;
            case SftpSymlink.NormalFile:
              this.cmbSymlink.SelectedIndex=1;
              break;
          }
        }
      }
    }

    private void chkEnabled_CheckedChanged(object sender,EventArgs e) {
      if(accountData==null)return;
      accountData.Enabled=this.chkEnabled.Checked;
    }
    private void txtRootDir_TextChanged(object sender,EventArgs e){
      if(accountData==null)return;
      accountData.ServerRoot=this.txtRootDir.Text;
    }
    private void chkOffline_CheckedChanged(object sender,EventArgs e){
      if(accountData==null)return;
      accountData.Offline=this.chkOffline.Checked;
    }
    private void chkReadonly_CheckedChanged(object sender,EventArgs e){
      if(accountData==null)return;
      accountData.ReadOnly=this.chkReadonly.Checked;
    }
    private void numReconnectCount_ValueChanged(object sender,EventArgs e) {
      if(accountData==null)return;
      accountData.ReconnectCount=(int)this.numReconnectCount.Value;
    }
    private void numDisconnectInt_ValueChanged(object sender,EventArgs e) {
      if(accountData==null)return;
      accountData.DisconnectInterval=(int)this.numDisconnectInt.Value;
    }
    private void numHeartbeat_ValueChanged(object sender,EventArgs e) {
      if(accountData==null)return;
      accountData.HeartbeatInterval=(int)this.numHeartbeat.Value;
    }
    private void cmbSymlink_SelectedIndexChanged(object sender,EventArgs e) {
      if(accountData==null)return;
      switch(cmbSymlink.SelectedIndex){
        case 0:
          this.accountData.SymlinkTreatment=SftpSymlink.Dereference;
          break;
        case 1:
          this.accountData.SymlinkTreatment=SftpSymlink.NormalFile;
          break;
      }
    }

  }
}

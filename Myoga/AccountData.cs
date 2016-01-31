using Gen=System.Collections.Generic;
using Forms=System.Windows.Forms;
using Ser=System.Runtime.Serialization;
using SerializationInfoReader=mwg.Mounter.Serialization.SerializationInfoReader;

namespace mwg.Sshfs{
	public enum SftpSymlink{
		Dereference,
		Shortcut,
		NormalFile,
	}

	public interface ISftpAccount:mwg.Mounter.IFsAccount{
		ISshSession CreateSession();

		string ServerRoot{get;}      // ルートにするサーバ側ディレクトリ
		bool Offline{get;}           // オフライン表示
		bool ReadOnly{get;}          // 読み取り専用
		int ReconnectCount{get;}     // 再接続試行回数
		int DisconnectInterval{get;} // 負値: 常時接続
		int HeartbeatInterval{get;}  // 負値: Heartbeat を送らない
		SftpSymlink SymlinkTreatment{get;}

    bool Enabled{get;} // 有効・無効
	}

	[System.Serializable]
	public class SshUserData:Ser::ISerializable{
		public string user;
		public string host;
		public int    port;

		public bool		useIdentityFile;
		public string pass;
		public string idtt;
		public string psph;

		public SshUserData(){
			this.user="user";
			this.host="host";
			this.port=22;
			this.pass="";
			this.idtt="";
			this.psph="";

			this.mute_fullname=this.FullName;
		}
		public SshUserData(string user,string host,string pass):this(){
			this.user=user;
			this.host=host;
			this.pass=pass;
		}

		public string Prompt{
			get{
				int i=host.IndexOf('.');
				string h=i>=0?host.Substring(0,i):host;
				return "["+user+"@"+h+"]";
			}
		}

		public string Password{
			get{return this.pass;}
			set{this.pass=value;}
		}
		//==========================================================================
		// user@host:port 形式での設定
		//--------------------------------------------------------------------------
		public string FullName{
			get{return this.user+"@"+this.host+":"+this.port.ToString();}
		}
		public bool TrySetFullName(string fullname){
			// user
			int i=fullname.IndexOf('@');
			if(i<0)return false;
			string user=fullname.Substring(0,i);
			string host=fullname.Substring(i+1);

			// port
			int port=22;
			i=host.IndexOf(':');
			if(i>=0){
				if(!int.TryParse(host.Substring(i+1),out port))return false;
				host=host.Substring(0,i);
			}

			if(user.Length==0||host.Length==0||port<0)return false;
			this.host=host;
			this.user=user;
			this.port=port;
			return true;
		}
		//==========================================================================
		// user@host:port 形式での編集用
		//--------------------------------------------------------------------------
		private string mute_fullname=null;
		public bool FullnameForEditIsValid{
			get{return this.mute_fullname==this.FullName;}
		}
		public string FullnameForEdit{
			get{return mute_fullname??(this.mute_fullname=this.FullName);}
			set{
				this.mute_fullname=value;
				if(this.TrySetFullName(value))
					mute_fullname=this.FullName;
			}
		}

		public override string ToString() {
			return this.host;
		}

		#region ISerializable メンバ
		SshUserData(Ser::SerializationInfo info,Ser::StreamingContext context){
			SerializationInfoReader reader=new SerializationInfoReader(info);
			reader.GetValue("user",out this.user);
			reader.GetValue("host",out this.host);
			reader.GetValue("port",out this.port);
			reader.GetValue("pass",out this.pass,"");
			reader.GetValue("idtt",out this.idtt,"");
			reader.GetValue("psph",out this.psph,"");
			reader.GetValue("useIdentityFile",out this.useIdentityFile,false);
		}
		void Ser::ISerializable.GetObjectData(Ser::SerializationInfo info,Ser::StreamingContext context){
			info.AddValue("user",this.user);
			info.AddValue("host",this.host);
			info.AddValue("port",this.port);
			info.AddValue("pass",this.pass);
			info.AddValue("idtt",this.idtt);
			info.AddValue("psph",this.psph);
			info.AddValue("useIdentityFile",this.useIdentityFile);
		}
		#endregion
	}
}

namespace mwg.Sshfs{
	using Ref=System.Reflection;
	using Gen=System.Collections.Generic;
	using Diag=System.Diagnostics;
	using Compiler=System.Runtime.CompilerServices;
	using Ser=System.Runtime.Serialization;
	using jsch=Tamir.SharpSsh.jsch;
	using Dk=Dokan;

	using SerializationInfoReader=mwg.Mounter.Serialization.SerializationInfoReader;


	[System.Serializable]
	internal class SftpAccount
		:Ser::ISerializable,ISftpAccount,mwg.Mounter.IFsAccount,
		UserInterface.ISftpAccountCommonSetting
	{
		string name;
		internal SshUserData data;

		public SftpAccount(string name){
			this.name=name;
			this.data=new SshUserData();
		}
		public SftpAccount(string name,SshUserData data){
			this.name=name;
			this.data=data;
		}

		public string Name{
			get{return this.name;}
			set{this.name=value;}
		}
		public ISshSession CreateSession(){
			return new SshSession(data,this);
		}

		public override string ToString(){
			return "[mwg.Sshfs.SftpAccount] "+this.name;
		}
		//==========================================================================
		// Settings
		//==========================================================================
		bool s_offline=false;
		string rootdir=".";
		bool s_readonly=false;
		int s_reconnect_count=1;
		int s_discon_interval=300;
		int s_beat_interval=60;
		SftpSymlink symlink=SftpSymlink.Dereference;
    bool s_enabled=true;

		public string ServerRoot{
			get{return this.rootdir;}
			set{this.rootdir=value;}
		}
		public bool Offline{
			get{return this.s_offline;}
			set{this.s_offline=value;}
		}
		public bool ReadOnly{
			get{return this.s_readonly;}
			set{this.s_readonly=value;}
		}
		public int ReconnectCount{
			get{return this.s_reconnect_count;}
			set{this.s_reconnect_count=value;}
		}
		public int DisconnectInterval{
			get{return this.s_discon_interval;}
			set{this.s_discon_interval=value;}
		}
		public int HeartbeatInterval{
			get{return this.s_beat_interval;}
			set{this.s_beat_interval=value;}
		}
		public SftpSymlink SymlinkTreatment{
			get{return this.symlink;}
			set{this.symlink=value;}
		}
    public bool Enabled{
      get{return this.s_enabled;}
      set{this.s_enabled=value;}
    }
		//==========================================================================
		// ISerializable
		//==========================================================================
		SftpAccount(Ser::SerializationInfo info,Ser::StreamingContext context){
			this.name=info.GetString("name");
			this.data=(SshUserData)info.GetValue("data",typeof(SshUserData));

			SerializationInfoReader reader=new SerializationInfoReader(info);
			reader.GetValue("offline",out this.s_offline,false);
			reader.GetValue("rootdir",out this.rootdir,".");
			reader.GetValue("s_readonly",out this.s_readonly,false);
			reader.GetValue("s_reconnect_count",out this.s_reconnect_count,1);
			reader.GetValue("s_discon_interval",out this.s_discon_interval,300);
			reader.GetValue("s_beat_interval",out this.s_beat_interval,60);
			int symlink;
			if(reader.GetValue("symlink",out symlink))
				this.symlink=(SftpSymlink)symlink;
			reader.GetValue("s_enabled",out this.s_enabled,true);
		}
		void Ser::ISerializable.GetObjectData(Ser::SerializationInfo info,Ser::StreamingContext context){
			info.AddValue("name",this.name);
			info.AddValue("data",this.data);
			info.AddValue("offline",this.s_offline);
			info.AddValue("rootdir",this.rootdir);
			info.AddValue("s_readonly",this.s_readonly);
			info.AddValue("s_reconnect_count",this.s_reconnect_count);
			info.AddValue("s_discon_interval",this.s_discon_interval);
			info.AddValue("s_beat_interval",this.s_beat_interval);
			info.AddValue("symlink",(int)this.symlink);
			info.AddValue("s_enabled",this.s_enabled);
		}

		#region UserInterface.IAccount
		mwg.Mounter.IAccountEditor mwg.Mounter.IFsAccount.CreateEditorInstance(){
			return new UserInterface.SftpAccountEditor();
		}
		#endregion
	}

	class SshSession:SshSessionBase,ISshSession{
		public SshSession(SshUserData data,ISftpAccount account):base(account,data.Prompt){
			this.data=data;
			this.config=new System.Collections.Hashtable(SSH_CONFIG);
		}
		//--------------------------------------------------------------------------
		//	設定
		//--------------------------------------------------------------------------
		public System.Collections.Hashtable SshConfig{
			get{return this.config;}
		}
		System.Collections.Hashtable config;

		jsch::JSch m_jsch;
		SshUserData data;
		//--------------------------------------------------------------------------
		//	接続・切断
		//--------------------------------------------------------------------------
		protected override jsch::Session ConnectImpl(){
			msg.Write(1,". Connecting...");
			this.m_jsch=new jsch::JSch();

			if(data.useIdentityFile&&data.idtt!="")
				this.m_jsch.addIdentity(data.idtt,data.psph);

			jsch::Session sess=this.m_jsch.getSession(data.user,data.host,data.port);
			sess=this.m_jsch.getSession(data.user,data.host,data.port);
			sess.setConfig(this.config);
			sess.setUserInfo(new SshLoginInfo(this.msg,this.data));
			sess.setPassword(data.pass);
			sess.connect();
			this.resources.Add(sess);

			msg.Write(1,". Connected");
			return sess;
		}
		protected override void DisconnectImpl(){
			this.m_jsch=null;
		}
	}
}

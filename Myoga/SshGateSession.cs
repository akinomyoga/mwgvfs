namespace mwg.Sshfs{
	using Ref=System.Reflection;
	using Gen=System.Collections.Generic;
	using Diag=System.Diagnostics;
	using Compiler=System.Runtime.CompilerServices;
	using Ser=System.Runtime.Serialization;
	using jsch=Tamir.SharpSsh.jsch;

	using SerializationInfoReader=mwg.Mounter.Serialization.SerializationInfoReader;

	[System.Serializable]
	internal class SftpAccountGw
		:Ser::ISerializable,ISftpAccount,
		mwg.Mounter.IFsAccount,
		UserInterface.ISftpAccountCommonSetting
	{
		string name;
		internal Gen::List<SshUserData> gwchain
			=new Gen::List<SshUserData>();
		public SftpAccountGw(string name){
			this.name=name;
		}
		public void AddUserData(SshUserData user){
			this.gwchain.Add(user);
		}

		public string Name{
			get{return this.name;}
			set{this.name=value;}
		}
		public ISshSession CreateSession(){
			return new SshGateSession(gwchain,this);
		}
		public override string ToString(){
			return "[mwg.Sshfs.SftpGateAccount] "+this.name;
		}
		//==========================================================================
		// Settings
		//==========================================================================
		bool s_offline=false;
		string rootdir="/";
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
		SftpAccountGw(Ser::SerializationInfo info,Ser::StreamingContext context){
			this.name=info.GetString("name");
			this.gwchain=(Gen::List<SshUserData>)info.GetValue("gwchain",typeof(Gen::List<SshUserData>));

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
			info.AddValue("gwchain",this.gwchain);
			info.AddValue("offline",this.s_offline);
			info.AddValue("rootdir",this.rootdir);
			info.AddValue("s_readonly",this.s_readonly);
			info.AddValue("s_reconnect_count",this.s_reconnect_count);
			info.AddValue("s_discon_interval",this.s_discon_interval);
			info.AddValue("s_beat_interval",this.s_beat_interval);
			info.AddValue("symlink",(int)this.symlink);
			info.AddValue("s_enabled",this.s_enabled);
		}

		#region IAccount メンバ
		public mwg.Mounter.IAccountEditor CreateEditorInstance() {
			return new UserInterface.SftpAccountGwEditor();
		}
		#endregion
	}

	class SshGateSession:SshSessionBase,ISshSession{
		//==========================================================================
		//	初期化
		//==========================================================================
		jsch::JSch m_jsch=null;
		Gen::List<SshUserData> gwchain;

		public SshGateSession(Gen::List<SshUserData> gwchain,ISftpAccount account)
			:base(account,init_GetPrompt(gwchain))
		{
			this.gwchain=gwchain;
		}
		static string init_GetPrompt(Gen::List<SshUserData> gwchain){
			int ilast=gwchain.Count-1;
			if(ilast<0)throw new System.ArgumentException("gwchain には要素が含まれていません。","gwchain");
			return gwchain[ilast].Prompt;
		}
		public SshGateSession(SshUserData user1,SshUserData user2,ISftpAccount account)
			:base(account,user2.Prompt)
		{
			this.gwchain=new Gen::List<SshUserData>();
			this.gwchain.Add(user1);
			this.gwchain.Add(user2);
		}
		//==========================================================================
		//	接続と切断
		//==========================================================================
		jsch::Session create_session(SshUserData user,jsch::Session gateway){
			msg.Write(1,". Connecting {0}@{1}",user.user,user.host);

			// setting
			jsch::Session session=m_jsch.getSession(user.user,user.host,user.port);
			session.setConfig(SSH_CONFIG);
			session.setUserInfo(new SshLoginInfo(this.msg,user));
			session.setPassword(user.pass);
			if(gateway!=null){
				GatewayProxy proxy=new GatewayProxy(gateway);
				resources.Add(proxy);
				session.setProxy(proxy);
			}

			// connect
			session.connect();
			resources.Add(session);
			return session;
		}
		protected override jsch::Session ConnectImpl(){
			if(this.gwchain.Count==0)return null;
			// register identities
			this.m_jsch=new jsch::JSch();
			foreach(SshUserData user in gwchain){
				if(user.useIdentityFile&&user.idtt!="")
					m_jsch.addIdentity(user.idtt,user.psph);
			}

			// logins
			jsch::Session gwsess=null;
			foreach(SshUserData user in gwchain){
				gwsess=create_session(user,gwsess);
			}
			
			return gwsess;
		}
		protected override void DisconnectImpl(){}

		//==========================================================================
		//	補助クラス
		//==========================================================================
		class GatewayProxy:jsch::Proxy{
			jsch::ChannelExec ch_e;
			System.IO.Stream istr;
			System.IO.Stream ostr;

			public GatewayProxy(jsch::Session parent){
				ch_e=(jsch::ChannelExec)parent.openChannel("exec");
			}

			void Tamir.SharpSsh.jsch.Proxy.close(){
				ch_e.disconnect();
			}
			void Tamir.SharpSsh.jsch.Proxy.connect(jsch::SocketFactory socket_factory,Tamir.SharpSsh.java.String host,int port,int timeout){
				ch_e.setCommand(string.Format("nc {0} {1}",host,port));
				istr=ch_e.getInputStream();  // 向こう→こっち
				ostr=ch_e.getOutputStream(); // こっち→向こう
				ch_e.connect();
			}

			System.IO.Stream Tamir.SharpSsh.jsch.Proxy.getInputStream(){
				return this.istr;
			}

			System.IO.Stream Tamir.SharpSsh.jsch.Proxy.getOutputStream() {
				return this.ostr;
			}

			Tamir.SharpSsh.java.net.Socket Tamir.SharpSsh.jsch.Proxy.getSocket(){
				return null;
			}
		}
	}
}

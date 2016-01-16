namespace mwg.Sshfs{
	using Ref=System.Reflection;
	using Gen=System.Collections.Generic;
	using Diag=System.Diagnostics;
	using Compiler=System.Runtime.CompilerServices;
	using Ser=System.Runtime.Serialization;
	using jsch=Tamir.SharpSsh.jsch;
	using Dk=Dokan;

	using SerializationInfoReader=mwg.Mounter.Serialization.SerializationInfoReader;

	public class SshfsMessage{
		public SshfsMessage(string prompt){
			this.prompt=prompt;
		}
		//--------------------------------------------------------------------------
		//	設定
		//--------------------------------------------------------------------------
		int verbose=0;
		public int VerboseLevel{
			get{return this.verbose;}
			set{this.verbose=value;}
		}
		string prompt="mwg.Sshfs";
		public string Prompt{
			get{return this.prompt;}
			set{if(value!=null)this.prompt=value;}
		}
		//--------------------------------------------------------------------------
		//	出力
		//--------------------------------------------------------------------------
		public void Write(int level,string msg){
			if(level>verbose)return;
			System.Console.WriteLine("{0}{1}",prompt,msg);
		}
		public void Write(int level,string msg,params object[] args){
			if(level>verbose)return;
			this.Write(level,string.Format(msg,args));
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		/// <returns>
		/// 接続が切れている事に依る例外の場合 1 を返します。
		/// それ以外の場合には -1 を返します。
		/// </returns>
		[Compiler::MethodImpl(Compiler::MethodImplOptions.NoInlining)]
		public int ReportErr(System.Exception e){
			if(e is jsch::SftpException){
				jsch::SftpException e1=(jsch::SftpException)e;
				this.Write(1,"! sftp: {0}",e1.message);
				switch((Unix.SSH_ERROR)e1.id){
					case Unix.SSH_ERROR.PERMISSION_DENIED:
						return -WinErrorCode.ERROR_ACCESS_DENIED;
					default:
						return -1;
				}
			}else{
				Diag::StackFrame callerFrame=new Diag::StackFrame(1);
				Ref::MethodBase caller=callerFrame.GetMethod();
				if(e is FailedToEstablishChannelException){
					this.Write(0,"! Failed to establish a channel @ ",caller.Name);
				}else{
					this.Write(0,"!!ERR!! at {0}\n{1}\n !!!!!!!",caller.Name,e);
				}
				return 1;
			}
		}
	}

	class FailedToEstablishChannelException:System.Exception{
		public FailedToEstablishChannelException(){}
	}

	class SshLoginInfo:jsch::UserInfo,jsch::UIKeyboardInteractive{
		SshfsMessage msg;
		string pass;
		string psph;

		public SshLoginInfo(SshfsMessage message,SshUserData user){
			this.msg=message;
			this.pass=user.pass;
			this.psph=user.psph;
		}

		string Tamir.SharpSsh.jsch.UserInfo.getPassphrase(){
			return this.psph;
		}

		string Tamir.SharpSsh.jsch.UserInfo.getPassword(){
			return this.pass;
		}

		bool Tamir.SharpSsh.jsch.UserInfo.promptPassphrase(string message){
			return false;
		}

		bool Tamir.SharpSsh.jsch.UserInfo.promptPassword(string message){
			return false;
		}

		bool Tamir.SharpSsh.jsch.UserInfo.promptYesNo(string message){
			return true;
		}

		void Tamir.SharpSsh.jsch.UserInfo.showMessage(string message){
			msg.Write(0,"% {0}",message.Trim());
		}

		string[] Tamir.SharpSsh.jsch.UIKeyboardInteractive.promptKeyboardInteractive(string destination,string name,string instruction,string[] prompt,bool[] echo) {
			return new string[]{this.pass};
		}
	}

	public interface ISshSession{
		jsch::ChannelSftp Sftp{get;}
		string Exec(string command);
		SshfsMessage Message{get;}
		bool Connect();
		bool Reconnect();
		void Disconnect();
		bool IsAlive{get;}
	}

	abstract class SshSessionBase:ISshSession{
		//==========================================================================
		//	静的メンバ
		//==========================================================================
		protected static System.Collections.Hashtable SSH_CONFIG
			=new System.Collections.Hashtable();
		static SshSessionBase(){
			SSH_CONFIG["StrictHostKeyChecking"]="no";
		}
		//==========================================================================
		//	メンバ
		//==========================================================================
		protected jsch::Session sess=null;
		protected SshfsMessage msg;
		protected ResourceList resources;
		protected ISftpAccount account;
		protected System.DateTime lastBeat;
		protected System.DateTime lastOperation;
		protected object sync_connect=new object();

		public SshfsMessage Message{
			get{return this.msg;}
		}

		//==========================================================================
		//	操作
		//==========================================================================
		protected SshSessionBase(ISftpAccount account,string prompt){
			this.msg=new SshfsMessage(prompt);
			this.resources=new ResourceList(this.msg);
			this.account=account;
		}

		Gen::Dictionary<int,jsch::ChannelSftp> channels
			=new Gen::Dictionary<int,jsch::ChannelSftp>();
		public jsch::ChannelSftp Sftp{
			get{
				if(!this.CheckConnect())return null;

				jsch::ChannelSftp ret=null;
				int k=System.Threading.Thread.CurrentThread.ManagedThreadId;
				if(!this.channels.TryGetValue(k,out ret)||!ret.isConnected()||ret.pwd()==null){
					if(ret!=null)try{ret.disconnect();}catch{}

					lock(this.sync_connect){
						ret=(jsch::ChannelSftp)this.sess.openChannel("sftp");

						//ret.connect();
						try{
							//const int TIMEOUT=3000;
							const int TIMEOUT=20000;
						  if(!Program.ExecTimeout.Execute(ret.connect,TIMEOUT))
								goto failed;
						}catch{
							goto failed;
						}

						if(ret.isConnected()&&ret.pwd()!=null){
							this.Message.Write(0,"D dbg20110712: ret.cwd={0}",ret.pwd());
							this.channels[k]=ret;
							resources.Add(ret);
						}else{
							this.Message.Write(0,"D dbg20111217: Failed to connect!");
							try{ret.disconnect();}catch{}
							goto failed;
						}
					}
				}
				return ret;
			failed:
				throw new FailedToEstablishChannelException();
			}
		}
		public string Exec(string command){
			msg.Write(2,"$ ssh "+command);

			jsch::ChannelExec ch_e=(jsch::ChannelExec)sess.openChannel("exec");
			System.IO.Stream istr=null;
			bool connected=false;
			System.IO.StreamReader sr=null;
			try{
				// 接続
				ch_e.setCommand(command);
				istr=ch_e.getInputStream();
				ch_e.connect();
				connected=true;

				// 読取
				sr=new System.IO.StreamReader(new PatchedReadStream(istr));
				return sr.ReadToEnd();
			}finally{
				if(sr!=null)sr.Close();
				if(connected)ch_e.disconnect();
				if(istr!=null)istr.Close();
			}
		}
		/// <summary>
		/// SharpSsh のストリームにバグがある様なので仕方なく仲介して回避。
		/// バグは具体的には、ストリーム末端に達した時に、
		/// ストリーム読み取りバイト数として 0 ではなく -1 を返すという物。
		/// </summary>
		class PatchedReadStream:System.IO.Stream{
			System.IO.Stream str;
			public PatchedReadStream(System.IO.Stream str){
				this.str=str;
			}

			public override bool CanRead{get{return true;}}
			public override void Flush(){str.Flush();}
			public override int Read(byte[] buffer,int offset,int count){
				int r=str.Read(buffer,offset,count);
				if(r<0)r=0;
				return r;
			}

			public override bool CanSeek{get{return false;}}
			public override bool CanWrite{get{return false;}}
			public override long Length{
				get{throw new System.NotImplementedException();}
			}
			public override long Position{
				get{throw new System.NotImplementedException();}
				set{throw new System.NotImplementedException();}
			}
			public override long Seek(long offset,System.IO.SeekOrigin origin) {
				throw new System.NotImplementedException();
			}
			public override void SetLength(long value) {
				throw new System.NotImplementedException();
			}
			public override void Write(byte[] buffer,int offset,int count) {
				throw new System.NotImplementedException();
			}
		}
		//==========================================================================
		//	接続と切断
		//==========================================================================
		protected abstract jsch::Session ConnectImpl();
		protected abstract void DisconnectImpl();
		public bool Connect(){
			lock(this.sync_connect){
				if(this.sess!=null)return true;

				try{
					this.sess=this.ConnectImpl();
					if(sess==null)return false;

					Program.Background+=this.connection_hold;
					return true;
				}catch(System.Exception e1){
					resources.Clear();
					msg.ReportErr(e1);
					return false;
				}
			}
		}
		public void Disconnect(){
			lock(this.sync_connect){
				if(this.sess==null)return;

				// 処理中断
				foreach(jsch::ChannelSftp channel in this.channels.Values)
					channel.exit();
				msg.Write(1,". Waiting...");
				System.Threading.Thread.Sleep(1000);

				// 接続切断
				msg.Write(1,". Disconnecting");
				Program.Background-=this.connection_hold;
				this.DisconnectImpl();

				// 資源解放
				resources.Clear();
				this.channels.Clear();
				this.sess=null;
				msg.Write(1,". Disconnected");
			}
		}
		public bool Reconnect(){
			try{
				this.Disconnect();
				return this.Connect();
			}catch(System.Exception e1){
				msg.ReportErr(e1);
				return false;
			}
		}
		//--------------------------------------------------------------------------
		public bool IsAlive{
			get{return this.sess!=null&&this.sess.isConnected();}
		}
		bool CheckConnect(){
			lastOperation=System.DateTime.Now;
			return this.Connect();
		}
		//--------------------------------------------------------------------------
		void connection_hold(){
			if(this.sess==null)return;

			System.DateTime dtnow=System.DateTime.Now;

			// 自動切断
			int idiscon=account.DisconnectInterval;
			if(idiscon>0&&dtnow>lastOperation.AddSeconds(idiscon)){
				this.Disconnect();
				return;
			}

			// Heartbeat 送信
			int iheart=account.HeartbeatInterval;
			if(iheart>0){
				System.DateTime dtth=dtnow.AddSeconds(-iheart);
				if(dtth>lastBeat&&dtth>lastOperation){
					this.msg.Write(2,". SSH_MSG_IGNORE");
          try{this.sess.sendIgnore();}catch{}
					this.lastBeat=dtnow;
				}
			}
		}
		//==========================================================================
		//	リソース管理
		//==========================================================================
		protected class ResourceList:System.IDisposable{
			Gen::List<System.IDisposable> list
				=new System.Collections.Generic.List<System.IDisposable>();
			SshfsMessage msg;
			public ResourceList(SshfsMessage msg){
				this.msg=msg;
			}

			public void Add(System.IDisposable value){
				lock(list)list.Add(value);
			}
			public void Add(jsch::Channel channel){
				this.Add(new ChannelDisposer(channel));
			}
			public void Add(jsch::Session session){
				this.Add(new SessionDisposer(session));
			}
			public void Add(jsch::Proxy proxy){
				this.Add(new ProxyDisposer(proxy));
			}
			public void Clear(){
				lock(list){
					for(int i=list.Count-1;i>=0;i--)
						try{
							System.IDisposable disp=list[i];
							if(disp!=null)disp.Dispose();
						}catch(System.Exception e0){
							msg.ReportErr(e0);
						}
					list.Clear();
				}
			}

			void System.IDisposable.Dispose() {
				this.Clear();
			}
		}
		class SessionDisposer:System.IDisposable{
			public jsch::Session obj;
			public SessionDisposer(jsch::Session obj){
				this.obj=obj;
			}
			void System.IDisposable.Dispose(){
				obj.disconnect();
			}
		}
		class ChannelDisposer:System.IDisposable{
			public jsch::Channel obj;
			public ChannelDisposer(jsch::Channel obj){
				this.obj=obj;
			}
			void System.IDisposable.Dispose(){
				obj.disconnect();
			}
		}
		class ProxyDisposer:System.IDisposable{
			public jsch::Proxy obj;
			public ProxyDisposer(jsch::Proxy obj){
				this.obj=obj;
			}
			void System.IDisposable.Dispose(){
				obj.close();
			}
		}
	}
}

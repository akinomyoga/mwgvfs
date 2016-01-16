using jsch=Tamir.SharpSsh.jsch;
using Gen=System.Collections.Generic;
using Dk=Dokan;

using Ref=System.Reflection;
using Diag=System.Diagnostics;
using Compiler=System.Runtime.CompilerServices;

namespace mwg {
  #region [削除] アカウント作成 2012/08/07 11:44:24
  partial class Progam{
    static Gen::Dictionary<string,string> passwd=new Gen::Dictionary<string,string>();
    static void InitPasswords(){
      passwd["tkynt"] ="";
      passwd["icepp"] ="";
      passwd["koma"]  ="";
      passwd["ecc.m1"]="";
      passwd["ecc.b4"]="";
      passwd["exp-xs"]="";
    }
    static void add_accounts(){
      ProgramSetting setting=new ProgramSetting();
      SshUserData user;
      user=new SshUserData("murase","tkyntm.phys.s.u-tokyo.ac.jp",passwd["tkynt"]);
      setting.AddAccount(new SftpAccount("tkyntm",user));
      user=new SshUserData("murase","tkynt2.phys.s.u-tokyo.ac.jp",passwd["tkynt"]);
      setting.AddAccount(new SftpAccount("tkynt2",user));
      user=new SshUserData("k-murase","polaris.icepp.s.u-tokyo.ac.jp",passwd["icepp"]);
      setting.AddAccount(new SftpAccount("polaris",user));
      user=new SshUserData("murase","kocoa.icepp.s.u-tokyo.ac.jp",passwd["koma"]);
      setting.AddAccount(new SftpAccount("kocoa",user));
      user=new SshUserData("ss106100","un001.ecc.u-tokyo.ac.jp",passwd["ecc.m1"]);
      setting.AddAccount(new SftpAccount("un001",user));
      user=new SshUserData("ss106100","un002.ecc.u-tokyo.ac.jp",passwd["ecc.m1"]);
      setting.AddAccount(new SftpAccount("un002",user));
      user=new SshUserData("ss106100","un003.ecc.u-tokyo.ac.jp",passwd["ecc.m1"]);
      setting.AddAccount(new SftpAccount("un003",user));

      SftpAccountGw acc;
      acc=new SftpAccountGw("murase@exp-xs00");
      acc.AddUserData(user);
      acc.AddUserData(new SshUserData("murase","exp-xs00.phys.s.u-tokyo.ac.jp",passwd["ecc.b4"]));
      setting.AddAccount(acc);

      acc=new SftpAccountGw("root@exp-xs00");
      acc.AddUserData(user);
      acc.AddUserData(new SshUserData("root","exp-xs00.phys.s.u-tokyo.ac.jp",passwd["exp-xs"]));
      setting.AddAccount(acc);

      acc=new SftpAccountGw("jlclogin2");
      acc.AddUserData(new SshUserData("k-murase","polaris.icepp.s.u-tokyo.ac.jp",passwd["icepp"]));
      acc.AddUserData(new SshUserData("k-murase","jlcgate.kek.jp",passwd["icepp"]));
      acc.AddUserData(new SshUserData("k-murase","jlclogin2.kek.jp",passwd["icepp"]));
      setting.AddAccount(acc);

      ProgramSetting.Save(setting);
    }
  }

  #endregion

  #region [削除] CacheFileData, CacheFileOps 13:28 2010/08/01
  // 13:28 2010/08/01
	class CacheFileData{
		public class Bucket{
			public System.DateTime date;
			public byte[] buff;
			public uint size;
			public bool dirty;

			public Bucket(){
				this.date=System.DateTime.MaxValue;
				this.buff=null;
				this.size=0;
				this.dirty=false;
			}
		}

		public const int SZ_BLOCK=0x4000;
		public const int N_BLOCK =8;
		public static System.TimeSpan TIMEOUT=new System.TimeSpan(0,0,5);
		static Bucket EMPTY=new Bucket();

		object sync_root=new object();
		int nref=0;
		Gen::Dictionary<long,Bucket> data=new Gen::Dictionary<long,Bucket>();
		//--------------------------------------------------------------------------
		//	参照カウント
		//--------------------------------------------------------------------------
		public bool Increment(){
			this.nref++;
			return true;
		}
		public bool Decrement(){
			this.nref--;
			bool ret=this.nref>0;
			if(!ret){
				this.FlushBack();
				this.data.Clear();
			}
			return ret;
		}
		//--------------------------------------------------------------------------
		//	データ読み書き
		//--------------------------------------------------------------------------
		public bool FlushBack(){
			lock(this.sync_root){
				bool success=true;
				foreach(long iblock in this.data.Keys){
					if(!this.FlushBack(iblock))
						success=false;
				}
				return success;
			}
		}
		private bool FlushBack(long iblock){
			Bucket bucket=this.data[iblock];
			if(!bucket.dirty)return true;
			bucket.dirty=false;
			// TODO: WriteFile
			return false;
		}
		//--------------------------------------------------------------------------
		//	データ取得・設定
		//--------------------------------------------------------------------------
		public bool TryGetBuck(long iblock,out Bucket buck,System.DateTime kigen){
			if(this.data.TryGetValue(iblock,out buck)){
				if(buck.date>=kigen){
					return true;
				}
			}

			buck=null;
			return false;
		}
		public bool TryGetBuff(long iblock,out byte[] buff,System.DateTime kigen){
			Bucket bucket;
			if(this.TryGetBuck(iblock,out bucket,kigen)){
				buff=bucket.buff;
				return true;
			}else{
				buff=null;
				return false;
			}
		}
		public Bucket GetBuck(long iblock){
			lock(sync_root){
				Bucket bucket;

				if(this.data.TryGetValue(iblock,out bucket)){
					// 既存セル
				}else if(this.data.Count>=SZ_BLOCK){
					// セル交換
					Gen::KeyValuePair<long,Bucket> repl=new Gen::KeyValuePair<long,Bucket>(-1,EMPTY);
					foreach(Gen::KeyValuePair<long,Bucket> pair in this.data){
						if(pair.Value.date<repl.Value.date)repl=pair;
					}

					this.FlushBack(repl.Key);
					this.data.Remove(repl.Key);
					bucket=repl.Value;
				}else{
					// 新規セル
					bucket=new Bucket();
					bucket.buff=new byte[SZ_BLOCK];
				}

				bucket.date=System.DateTime.Now;
				this.data[iblock]=bucket;
				return bucket;
			}
		}
		public byte[] GetBuff(long iblock){
			return this.GetBuck(iblock).buff;
		}
		public void SetBuff(long iblock,byte[] buff){
			System.Array.Copy(buff,this.GetBuff(iblock),SZ_BLOCK);
		}
	}
	class CacheFileOps:Dokan.DokanOperations{
		Dokan.DokanOperations ops;
		Gen::Dictionary<string,CacheFileData> filecache
			=new Gen::Dictionary<string,CacheFileData>();

		public CacheFileOps(Dokan.DokanOperations ops){
			this.ops=ops;
		}

		int DokanOperations.CreateFile(string filename,FileAccess access,FileShare share,FileMode mode,FileOptions options,DokanFileInfo info) {
			int ret=ops.CreateFile(filename,access,share,mode,options,info);
			if(ret==0){
				string k=filename.ToLower();
				if(!this.filecache.ContainsKey(k))
					this.filecache[k]=new CacheFileData();
				this.filecache[k].Increment();
			}
			return ret;
		}
		int DokanOperations.Cleanup(string filename,DokanFileInfo info) {
			CacheFileData cache;
			if(this.filecache.TryGetValue(filename.ToLower(),out cache)){
				cache.FlushBack();
				return ops.Cleanup(filename,info);
			}else{
				return -1;
			}
		}
		int DokanOperations.CloseFile(string filename,DokanFileInfo info){
			CacheFileData cache;
			if(this.filecache.TryGetValue(filename.ToLower(),out cache)){
				if(!cache.Decrement()){
					this.filecache.Remove(filename);
				}
				return ops.CloseFile(filename,info);
			}else{
				return -1;
			}
		}
		int DokanOperations.FlushFileBuffers(string filename,DokanFileInfo info) {
			CacheFileData cache;
			if(this.filecache.TryGetValue(filename.ToLower(),out cache)){
				cache.FlushBack();
				return ops.FlushFileBuffers(filename,info);
			}else{
				return -1;
			}
		}
		//==========================================================================
		int DokanOperations.ReadFile(string filename,byte[] buffer,ref uint mbuffer,long offset,DokanFileInfo info){
			// TEST
			return ops.ReadFile(filename,buffer,ref mbuffer,offset,info);

			const int SZ_BLOCK=CacheFileData.SZ_BLOCK;
			CacheFileData cache;
			if(!this.filecache.TryGetValue(filename.ToLower(),out cache))return -1;

			if(mbuffer==0)mbuffer=(uint)buffer.Length;
			uint ibuffer=0;
			long sblock=offset/SZ_BLOCK;
			long eblock=(offset+mbuffer+SZ_BLOCK-1)/SZ_BLOCK;
			offset-=SZ_BLOCK*sblock;

			//System.Console.WriteLine("- range {0:X}-{1:X} blocks {2}-{3}",offset,offset+mbuffer,sblock,eblock);

			// キャッシュがある間はそこから読み取る
			System.DateTime kigen=System.DateTime.Now-CacheFileData.TIMEOUT;
			for(;sblock<eblock;sblock++){
				CacheFileData.Bucket buck;
				if(!cache.TryGetBuck(sblock,out buck,kigen))break;

				if(this.ReadFromBuck(buck,ref offset,buffer,ref ibuffer,mbuffer)){
					mbuffer=ibuffer;
					return 0;
				}
			}

			if(sblock==eblock)return 0;

			// 読み取り
			{
				uint len=(uint)(eblock-sblock)*SZ_BLOCK;
				bool is_direct=len==SZ_BLOCK; // 丁度ブロックサイズの時は直接 cache に書き込む

				CacheFileData.Bucket buck;
				if(is_direct){
					buck=cache.GetBuck(sblock);
				}else{
					buck=new CacheFileData.Bucket();
					buck.buff=new byte[len];
				}

				int ret=this.ReadFromFile(buck,filename,sblock,len,info);
				if(ret!=0)return ret;

				this.ReadFromBuck(buck,ref offset,buffer,ref ibuffer,mbuffer);
				mbuffer=ibuffer;

				if(!is_direct)this.WriteToCache(cache,buck,sblock);
				return 0;
			}
		}
		#region ReadFile Helpers
		/// <summary>
		/// ファイルから bucket にデータを読み取ります。
		/// </summary>
		/// <param name="buck"></param>
		/// <param name="filename"></param>
		/// <param name="sblock"></param>
		/// <param name="len"></param>
		/// <param name="info"></param>
		/// <returns></returns>
		private int ReadFromFile(CacheFileData.Bucket buck,string filename,long sblock,uint len,DokanFileInfo info){
			const uint SZ_BLOCK=(uint)CacheFileData.SZ_BLOCK;

			int ret=ops.ReadFile(filename,buck.buff,ref len,sblock*SZ_BLOCK,info);
			if(ret!=0){
				buck.date=System.DateTime.MinValue;
				buck.size=len;
			}else{
				buck.size=len;
			}

			return ret;
		}
		/// <summary>
		/// 指定したデータをキャッシュに格納します。
		/// </summary>
		/// <param name="cache">キャッシュを指定します。。</param>
		/// <param name="buck">データを保持している bucket を指定します。</param>
		/// <param name="sblock">データのファイル上での位置を表す番号を指定します。</param>
		private void WriteToCache(CacheFileData cache,CacheFileData.Bucket buck,long sblock){
			const uint SZ_BLOCK=(uint)CacheFileData.SZ_BLOCK;
			const uint N_BLOCK=(uint)CacheFileData.N_BLOCK;

			long eblock=sblock+(buck.size+SZ_BLOCK-1)/SZ_BLOCK;
			long iblock=sblock;
			uint ibuff=0;

			// Skip
			if(iblock<eblock-N_BLOCK){
				iblock=eblock-N_BLOCK;
				ibuff=(uint)(iblock-sblock)*SZ_BLOCK;
				buck.size-=ibuff;
			}

			for(;iblock<eblock;iblock++){
				CacheFileData.Bucket buck1=cache.GetBuck(iblock);
				uint l=buck.size<SZ_BLOCK?buck.size:SZ_BLOCK;
				System.Array.Copy(buck.buff,ibuff,buck1.buff,0,l);
				ibuff+=l;
				buck.size-=l;
			}
		}
		/// <summary>
		/// 指定した bucket から buffer にデータを読み取ります。
		/// </summary>
		/// <param name="buck"></param>
		/// <param name="offset"></param>
		/// <param name="buffer"></param>
		/// <param name="ibuffer"></param>
		/// <returns>ファイルの末端に達し、これ以上読み取る必要が無くなった時に true を返します。</returns>
		private bool ReadFromBuck(CacheFileData.Bucket buck,ref long offset,byte[] buffer,ref uint ibuffer,uint mbuffer){
			int l1=(int)(buck.size-offset); // 読み取り可能な量
			int l2=(int)(mbuffer-ibuffer);  // 読み取りたい量
			int l=l1<l2?l1:l2;
			if(l>0){
				System.Array.Copy(buck.buff,offset,buffer,ibuffer,l);
				offset=0;
				ibuffer+=(uint)l;
			}

			// Completed! (or EOF)
			const int SZ_BLOCK=CacheFileData.SZ_BLOCK;
			return ibuffer==mbuffer||buck.size<SZ_BLOCK;
		}
		#endregion
		//==========================================================================
		int DokanOperations.WriteFile(string filename,byte[] buffer,ref uint writtenBytes,long offset,DokanFileInfo info) {
			return ops.WriteFile(filename,buffer,ref writtenBytes,offset,info);
			// TODO:
		}

		#region DokanOperations メンバ
		int DokanOperations.CreateDirectory(string filename,DokanFileInfo info) {
			return ops.CreateDirectory(filename,info);
		}
		int DokanOperations.DeleteDirectory(string filename,DokanFileInfo info) {
			return ops.DeleteDirectory(filename,info);
		}
		int DokanOperations.DeleteFile(string filename,DokanFileInfo info) {
			return ops.DeleteFile(filename,info);
		}
		int DokanOperations.FindFiles(string filename,System.Collections.ArrayList files,DokanFileInfo info) {
			return ops.FindFiles(filename,files,info);
		}
		int DokanOperations.GetDiskFreeSpace(ref ulong freeBytesAvailable,ref ulong totalBytes,ref ulong totalFreeBytes,DokanFileInfo info) {
			return ops.GetDiskFreeSpace(ref freeBytesAvailable,ref totalBytes,ref totalFreeBytes,info);
		}
		int DokanOperations.GetFileInformation(string filename,FileInformation fileinfo,DokanFileInfo info) {
			return ops.GetFileInformation(filename,fileinfo,info);
		}
		int DokanOperations.LockFile(string filename,long offset,long length,DokanFileInfo info) {
			return ops.LockFile(filename,offset,length,info);
		}
		int DokanOperations.MoveFile(string filename,string newname,bool replace,DokanFileInfo info) {
			return ops.MoveFile(filename,newname,replace,info);
		}
		int DokanOperations.OpenDirectory(string filename,DokanFileInfo info) {
			return ops.OpenDirectory(filename,info);
		}
		int DokanOperations.SetAllocationSize(string filename,long length,DokanFileInfo info) {
			return ops.SetAllocationSize(filename,length,info);
		}
		int DokanOperations.SetEndOfFile(string filename,long length,DokanFileInfo info) {
			return ops.SetEndOfFile(filename,length,info);
		}
		int DokanOperations.SetFileAttributes(string filename,FileAttributes attr,DokanFileInfo info) {
			return ops.SetFileAttributes(filename,attr,info);
		}
		int DokanOperations.SetFileTime(string filename,System.DateTime ctime,System.DateTime atime,System.DateTime mtime,DokanFileInfo info) {
			return ops.SetFileTime(filename,ctime,atime,mtime,info);
		}
		int DokanOperations.UnlockFile(string filename,long offset,long length,DokanFileInfo info) {
			return ops.UnlockFile(filename,offset,length,info);
		}
		int DokanOperations.Unmount(DokanFileInfo info) {
			return ops.Unmount(info);
		}
		#endregion
	}
	#endregion

	#region [書換] SshSession, SshGateSession -> SshSessionBase 07:21 2010/06/09
	// 07:21 2010/06/09
	class SshSession:ISshSession{
		static System.Collections.Hashtable SSH_CONFIG=new System.Collections.Hashtable();
		static SshSession(){
			SSH_CONFIG["StrictHostKeyChecking"]="no";
		}

		System.Collections.Hashtable config;

		ISftpAccount account;
		SshUserData data;
		protected object sync_connect=new object();

		jsch::JSch jsch;
		jsch::Session session;
		Gen::Dictionary<int,jsch::ChannelSftp> channels
			=new Gen::Dictionary<int,jsch::ChannelSftp>();

		private SshfsMessage msg;
		public SshfsMessage Message{
			get{return this.msg;}
		}

		public SshSession(SshUserData data){
			this.data=data;

			// Initialize Message
			this.msg=new SshfsMessage(data.Prompt);

			this.config=new System.Collections.Hashtable(SSH_CONFIG);
		}
		public jsch::ChannelSftp Sftp{
			get{
				if(!this.CheckConnect())return null;

				jsch::ChannelSftp ret;
				int k=System.Threading.Thread.CurrentThread.ManagedThreadId;
				if(!this.channels.TryGetValue(k,out ret)){
					lock(this.sync_connect){
						ret=(jsch::ChannelSftp)this.session.openChannel("sftp");
						ret.connect();
						this.channels[k]=ret;
					}
				}
				return ret;
			}
		}

		//--------------------------------------------------------------------------
		//	設定
		//--------------------------------------------------------------------------
		public System.Collections.Hashtable SshConfig{
			get{return this.config;}
		}
		//--------------------------------------------------------------------------
		//	接続・切断
		//--------------------------------------------------------------------------
		public bool IsAlive{
			get{return this.session!=null&&this.session.isConnected();}
		}
		bool CheckConnect(){
			return this.Connect();
			/*
			lock(this.sync_connect){
				if(this.IsAlive)return true;
				return this.SshReconnect();
			}
			//*/
		}
		//--------------------------------------------------------------------------

		public bool Reconnect(){
			// 二重実行防止
			try{
				// TODO: hogehoge
				this.Disconnect();
				return this.Connect();
			}catch(System.Exception e1){
				msg.ReportErr(e1);
				return false;
			}
		}
		public bool Connect(){
			lock(this.sync_connect){
				if(this.IsAlive)return true;

				try{
					msg.Write(1,". Connecting...");
					this.channels.Clear();
					this.jsch=new jsch::JSch();

					if(data.idtt!=null)
						this.jsch.addIdentity(data.idtt,data.psph);

					this.session=this.jsch.getSession(data.user,data.host,data.port);
					this.session.setConfig(this.config);
					this.session.setUserInfo(new SshLoginInfo(this.msg,this.data));
					this.session.setPassword(data.pass);
					this.session.connect();

					msg.Write(1,". Connected");
					return true;
				}catch(System.Exception e1){
					msg.ReportErr(e1);
					return false;
				}
			}
		}
		public void Disconnect(){
			lock(this.sync_connect){
				if(!this.IsAlive)return;

				msg.Write(1,". Disconnecting...");
				foreach(jsch::ChannelSftp channel in this.channels.Values){
					try{
						channel.disconnect();
					}catch(System.Exception e0){
						msg.ReportErr(e0);
					}
				}
				try{
					this.session.disconnect();
				}catch(System.Exception e1){
					msg.ReportErr(e1);
				}
				msg.Write(1,". Disconnected");

				this.channels.Clear();
				this.session=null;
				this.jsch=null;
			}
		}
	}

	// 07:09 2010/06/09
	class SshGateSession:ISshSession{
		//==========================================================================
		//	静的メンバ
		//==========================================================================
		static System.Collections.Hashtable SSH_CONFIG
			=new System.Collections.Hashtable();
		static SshGateSession(){
			SSH_CONFIG["StrictHostKeyChecking"]="no";
		}
		//==========================================================================
		//
		//==========================================================================
		Gen::List<SshUserData> gwchain;
		jsch::Session sess2=null;

		object sync_connect=new object();
		jsch::JSch m_jsch=new Tamir.SharpSsh.jsch.JSch();
		Gen::List<jsch::Session> sessions=new Gen::List<jsch::Session>();

		SshfsMessage msg;
		ResourceList resources;

		public SshfsMessage Message{
			get{return this.msg;}
		}

		public SshGateSession(Gen::List<SshUserData> gwchain,ISftpAccount account){
			int ilast=gwchain.Count-1;
			if(ilast<0)throw new System.ArgumentException("gwchain には要素が含まれていません。","gwchain");

			this.msg=new SshfsMessage(gwchain[ilast].Promt);
			this.resources=new ResourceList(this.msg);
			this.gwchain=gwchain;
			this.account=account;
		}
		public SshGateSession(SshUserData user1,SshUserData user2,ISftpAccount account){
			this.msg=new SshfsMessage(user2.Promt);
			this.resources=new ResourceList(this.msg);
			this.gwchain=new Gen::List<SshUserData>();
			this.gwchain.Add(user1);
			this.gwchain.Add(user2);
			this.account=account;
		}

		Gen::Dictionary<int,jsch::ChannelSftp> channels
			=new Gen::Dictionary<int,jsch::ChannelSftp>();
		public jsch::ChannelSftp Sftp{
			get{
				if(!this.CheckConnect())return null;

				jsch::ChannelSftp ret;
				int k=System.Threading.Thread.CurrentThread.ManagedThreadId;
				if(!this.channels.TryGetValue(k,out ret)){
					lock(this.sync_connect){
						ret=(jsch::ChannelSftp)this.sess2.openChannel("sftp");
						ret.connect();
						this.channels[k]=ret;
						resources.Add(ret);
					}
				}
				return ret;
			}
		}
		public bool IsAlive{
			get{return this.sess2!=null&&this.sess2.isConnected();}
		}
		bool CheckConnect(){
			lastOperation=System.DateTime.Now;
			return this.Connect();
		}
		//==========================================================================
		ISftpAccount account;
		System.DateTime lastBeat;
		System.DateTime lastOperation;
		void connection_hold(){
			if(this.sess2==null)return;

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
					this.sess2.sendIgnore();
					this.lastBeat=dtnow;
				}
			}
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
		public bool Connect(){
			lock(this.sync_connect){
				if(this.IsAlive)return true;

				if(this.gwchain.Count==0)return false;
				try{
					// register identities
					foreach(SshUserData user in gwchain){
						if(user.idtt!=null)
							m_jsch.addIdentity(user.idtt,user.psph);
					}

					// logins
					jsch::Session gwsess=null;
					foreach(SshUserData user in gwchain){
						gwsess=create_session(user,gwsess);
					}
					
					this.sess2=gwsess;
					return true;
				}catch(System.Exception e){
					resources.Clear();
					msg.ReportErr(e);
					return false;
				}
			}
		}
		public void Disconnect(){
			lock(this.sync_connect){
				if(!this.IsAlive)return;

				msg.Write(1,". Disconnecting");
				resources.Clear();
				this.channels.Clear();
				this.sess2=null;
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

		//==========================================================================
		//	補助クラス
		//==========================================================================
		class ResourceList:System.IDisposable{
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
	#endregion

	#region [削除] gateway 透過実験 13:35 2010/05/30
	// 13:35 2010/05/30
	class Program{
		public static void ssh_test(){
			SshUserData data1=new SshUserData();
			data1.user="ss106100";
			data1.host="un003.ecc.u-tokyo.ac.jp";
			data1.port=22;
			data1.pass=passwd["ecc.m1"];

			System.Collections.Hashtable config=new System.Collections.Hashtable();
			config["StrictHostKeyChecking"]="no";

			SshfsMessage mess1=new SshfsMessage("[]");

			jsch::JSch jsch=new Tamir.SharpSsh.jsch.JSch();
			jsch::Session sess1=jsch.getSession(data1.user,data1.host,data1.port);
			sess1.setConfig(config);
			//sess1.setUserInfo(new DokanSSHFS.DokanUserInfo(data1.pass,null));
			sess1.setUserInfo(new SshLoginInfo(mess1,data1));
			sess1.setPassword(data1.pass);
			sess1.connect();

			/*
			jsch::ChannelExec ch_e=(jsch::ChannelExec)sess1.openChannel("exec");
			ch_e.setCommand("ls -alB ~/CalculationTA");
			ch_e.setOutputStream(System.Console.OpenStandardOutput(),true);
			System.Console.WriteLine("ls -al ~/CalculationTA");
			ch_e.connect();
			ch_e.start();
			System.Threading.Thread.Sleep(1000);
			ch_e.disconnect();
			System.Console.WriteLine("comp.");
			//*/

			MyProx proxy=new MyProx(sess1);

			SshUserData data2=new SshUserData();
			data2.user="root";
			data2.host="exp-xs00.phys.s.u-tokyo.ac.jp";
			data2.port=22;
			data2.pass=passwd["exp-xs"];
			jsch::Session sess2=jsch.getSession(data2.user,data2.host,data2.port);
			sess2.setConfig(config);
			sess2.setUserInfo(new DokanSSHFS.DokanUserInfo(data2.pass,null));
			sess2.setPassword(data2.pass);
			sess2.setProxy(proxy);
			sess2.connect();

			jsch::ChannelExec ch_e=(jsch::ChannelExec)sess2.openChannel("exec");
			ch_e.setCommand("ls -alB ~/");
			ch_e.setOutputStream(System.Console.OpenStandardOutput(),true);
			System.Console.WriteLine("ls -al ~/");
			ch_e.connect();

			System.Threading.Thread.Sleep(2000);
			System.Console.WriteLine("comp.");
			ch_e.disconnect();
			sess2.disconnect();
			sess1.disconnect();
		}
		class MyProx:jsch::Proxy{
			jsch::ChannelExec ch_e;
			System.IO.Stream istr;
			System.IO.Stream ostr;

			public MyProx(jsch::Session parent){
				System.Console.WriteLine("ssh...");
				ch_e=(jsch::ChannelExec)parent.openChannel("exec");
			}

			#region Proxy メンバ

			void Tamir.SharpSsh.jsch.Proxy.close(){
				System.Console.WriteLine("MyProx.close");
				ch_e.disconnect();
			}

			void Tamir.SharpSsh.jsch.Proxy.connect(jsch::SocketFactory socket_factory,Tamir.SharpSsh.java.String host,int port,int timeout){
				System.Console.WriteLine("MyProx.connect(factory,host={0},port={1},timeout={2})",host,port,timeout);
				ch_e.setCommand(string.Format("nc {0} {1}",host,port));
				//istr=new StreamTee("istr",ch_e.getInputStream());  // 向こう→こっち
				//ostr=new StreamTee("ostr",ch_e.getOutputStream()); // こっち→向こう
				istr=ch_e.getInputStream();  // 向こう→こっち
				ostr=ch_e.getOutputStream(); // こっち→向こう
				ch_e.connect();
			}

			System.IO.Stream Tamir.SharpSsh.jsch.Proxy.getInputStream(){
				System.Console.WriteLine("MyProx.getInputStream");
				return this.istr;
			}

			System.IO.Stream Tamir.SharpSsh.jsch.Proxy.getOutputStream() {
				System.Console.WriteLine("MyProx.getOutputStream");
				return this.ostr;
			}

			Tamir.SharpSsh.java.net.Socket Tamir.SharpSsh.jsch.Proxy.getSocket(){
				System.Console.WriteLine("MyProx.getSocket");
				return null;
			}

			#endregion
		}
		class StreamTee:System.IO.Stream{
			string name;
			System.IO.Stream parent;
			System.IO.Stream output;
			public StreamTee(string name,System.IO.Stream parent){
				this.name=name;
				this.parent=parent;
				this.output=System.Console.OpenStandardOutput();
			}
			public System.IO.Stream ParentStream{
				get{return parent;}
			}

			public override int Read(byte[] buffer,int offset,int count){
				count=parent.Read(buffer,offset,count);
				if(count>0)output.Write(buffer,offset,count);
				return count;
			}
			public override int ReadByte(){
				int ch=parent.ReadByte();
				if(ch>=0)output.WriteByte((byte)ch);
				return ch;
			}
			public override void Write(byte[] buffer,int offset,int count){
				output.Write(buffer,offset,count);
				parent.Write(buffer,offset,count);
			}
			public override void WriteByte(byte value){
				output.WriteByte(value);
				parent.WriteByte(value);
			}

			public override bool CanRead{get{return parent.CanRead;}}
			public override bool CanSeek{get{return parent.CanSeek;}}
			public override bool CanWrite{get{return parent.CanWrite;}}

			public override void Flush(){parent.Flush();}
			public override long Length{get{return parent.Length;}}

			public override long Position{
				get{return parent.Position;}
				set{parent.Position=value;}
			}
			public override long Seek(long offset,System.IO.SeekOrigin origin){
				return parent.Seek(offset,origin);
			}
			public override void SetLength(long value){
				parent.SetLength(value);
			}
		}
	}
	#endregion

	// 22:25 2010/05/29
	class SshSession{
		string prompt;
		int verbose=0;
		public int VerboseLevel{
			get{return this.verbose;}
			set{this.verbose=value;}
		}

		//--------------------------------------------------------------------------
		//	デバグ用出力
		//--------------------------------------------------------------------------
		public void Message(int level,string msg){
			if(level>verbose)return;
			System.Console.WriteLine("{0}{1}",prompt,msg);
		}
		public void Message(int level,string msg,params object[] args){
			if(level>verbose)return;
			this.msg.Message(level,string.Format(msg,args));
		}
		[Compiler::MethodImpl(Compiler::MethodImplOptions.NoInlining)]
		public void MessageErr(System.Exception e){
			if(e is jsch::SftpException){
				jsch::SftpException e1=(jsch::SftpException)e;
				msg.Message(1,"! sftp: {0}",e1.message);
			}else{
				Diag::StackFrame callerFrame=new Diag::StackFrame(1);
				Ref::MethodBase caller=callerFrame.GetMethod();
				msg.Message(0,"!!ERR!! at {0}\n{1}\n !!!!!!!",caller.Name,e);
			}
		}

	}

	#region IFsEntry
	interface IFsEntry{
		Dk::FileInformation FileInfo{get;}
	}
	interface IFsDirectory:IFsEntry{
		Gen::IEnumerable<Dk::FileInformation> GetFiles();
		IFsEntry GetEntry(string name);
	}
	class SftpFile:IFsEntry{
		protected Dk::FileInformation info;
		public Dk::FileInformation FileInfo{
			get{return this.info;}
		}
		public SftpFile(Dk::FileInformation info){
			this.info=info;
		}
	}

	class SftpDirectory:SftpFile,IFsDirectory{
		SshSession session;
		string path_remote;

		// 情報を取得した日時を保持します。
		System.DateTime dt_update=System.DateTime.MinValue;
		Gen::List<Dk::FileInformation> filelist;
		Gen::Dictionary<string,IFsEntry> entries;
		IFsDirectory parent;

		bool s_offline=true;
		public bool IsOffline{
			get{return this.s_offline;}
			set{this.s_offline=value;}
		}

		public IFsEntry GetEntry(string name){
			return this.GetEntry(name,false);
		}
		public IFsEntry GetEntry(string name,bool update){
			if(name==".")return this;
			if(name=="..")return this.parent;

			IFsEntry ret;
			if(this.entries.TryGetValue(name,out ret))return ret;

			if(update){
				this.Update();
				if(this.entries.TryGetValue(name,out ret))return ret;
			}
			return null;
		}
		//==========================================================================
		//		初期化
		//==========================================================================
		public SftpDirectory(SftpDirectory parent,Dk::FileInformation info)
			:this(parent,info,parent.session,parent.path_remote)
		{
			this.s_offline=parent.s_offline;
		}
		public SftpDirectory(IFsDirectory parent,Dk::FileInformation info,SshSession session,string path_remote):base(info){
			this.parent=parent??this;
			this.session=session;
			this.path_remote=path_remote;
		}
		//==========================================================================
		//		ファイル一覧・情報
		//==========================================================================
		public int GetFileInfo(string filename,Dk::FileInformation fileinfo){
			// use cache
			IFsEntry ent;
			if(this.entries.TryGetValue(filename,out ent)){
				Dk::FileInformation info=ent.FileInfo;
				fileinfo.Attributes=info.Attributes;
				fileinfo.CreationTime=info.CreationTime;
				fileinfo.LastAccessTime=info.LastAccessTime;
				fileinfo.LastWriteTime=info.LastWriteTime;
				fileinfo.Length=info.Length;
				return 0;
			}

			try{
				this.SetFileInfo(fileinfo,session.Sftp.stat(this.path_remote+"/"+filename));
				return 0;
			}catch (jsch::SftpException e1){
				session.MessageErr(e1);
				return -1;
			}catch (System.Exception e2){
				session.MessageErr(e2);
				session.SshReconnect();
				return -1;
			}
		}

		private void SetFileInfo(Dk::FileInformation info,jsch::SftpATTRS r_attr){
			info.Length=r_attr.getSize();
			info.CreationTime=UNIX_TIMEBASE.AddSeconds(r_attr.getMTime());
			info.LastAccessTime=UNIX_TIMEBASE.AddSeconds(r_attr.getATime());
			info.LastWriteTime=UNIX_TIMEBASE.AddSeconds(r_attr.getMTime());

			// attributes
			info.Attributes=
				r_attr.isDir()?System.IO.FileAttributes.Directory:
				System.IO.FileAttributes.Normal;
			if(this.s_offline)
				info.Attributes|=System.IO.FileAttributes.Offline;
			if(info.FileName[0]=='.')
				info.Attributes|=System.IO.FileAttributes.Hidden;
		}

		public Gen::IEnumerable<Dk::FileInformation> GetFiles(){
			try{
				this.Update();
				return this.filelist;
			}catch(jsch::SftpException e1){
				session.MessageErr(e1);
				return null;
			}catch(System.Exception e2){
				session.MessageErr(e2);
			}

			// Reconnect & 再試行
			try{
				if(!session.SshReconnect())return null;
				this.Update();
				return this.filelist;
			}catch(System.Exception e3){
				session.MessageErr(e3);
				return null;
			}
		}
		void Update(){
			if(System.DateTime.Now<this.dt_update+TIMEOUT)return;

			Gen::List<Dk::FileInformation> newlist=new Gen::List<Dokan.FileInformation>();
			Gen::Dictionary<string,IFsEntry> newdic=new Gen::Dictionary<string,IFsEntry>();
			foreach(jsch::ChannelSftp.LsEntry entry in session.Sftp.ls(path_remote)){
				Dk::FileInformation info=new Dk::FileInformation();

				// file information
				info.FileName=(string)entry.getFilename();

				jsch::SftpATTRS r_attr=entry.getAttrs();
				this.SetFileInfo(info,r_attr);

				newlist.Add(info);

				IFsEntry fsentry;
				if(r_attr.isDir()){
					fsentry=new SftpDirectory(this,info);
				}else{
					fsentry=new SftpFile(info);
				}
				newdic.Add(info.FileName,fsentry);
			}

			// 置き換え
			filelist=newlist;
			entries=newdic;
			dt_update=System.DateTime.Now;
		}

		static System.TimeSpan TIMEOUT=new System.TimeSpan(0,0,5);
		static System.DateTime UNIX_TIMEBASE=new System.DateTime(1970,1,1,0,0,0,System.DateTimeKind.Utc);
	}
	#endregion

	#region DataCache
	unsafe interface IRemoteFile{
		long Size{get;}
		bool ReadOnly{get;}
		System.DateTime LastUpdate{get;}
		bool ReadData(byte* buff,long offset,int length);
		bool WriteData(byte* buff,long offset,int length);
	}

	unsafe interface ICacheBlock{
		long Offset{get;}
		int Length{get;}
		bool ReadData(byte[] buff,long offset,int length);
	}

	public class CacheBlock{
		byte[] buffer;
		long offset;
		int length;
		long Offset{get{return this.offset;}}
		int Length{get{return this.length;}}
		public void ReadData(byte[] buff,long offset,int length){
			long sstart=this.offset;
			long send=this.offset+this.length;
			long dstart=offset;
			long dend=offset+length;

			long start=sstart>dstart?sstart:offset;
			long end  =send  <dend  ?send  :dend  ;

			long len=end-start;
			if(len<=0)return;
			
			System.Array.Copy(this.buffer,start-sstart,buff,start-dstart,end-start);
		}
	}

	class DataCache{
		//Gen::Dictionary<IRemoteFile,CachePtr>;
		public void GetData(IRemoteFile file,byte[] buff,long offset,int length){
		}
	}
	#endregion


}

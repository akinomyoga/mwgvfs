using jsch=Tamir.SharpSsh.jsch;

using Environment=System.Environment;
using Application=System.Windows.Forms.Application;
using Gen=System.Collections.Generic;

namespace mwg.Sshfs{
	static class Program{
		static System.Threading.Thread bg_thread;
		internal static event System.Threading.ThreadStart Background;

		static void bg_work(){
			while(true){
				if(Background!=null)Background();
				System.Threading.Thread.Sleep(10*1000);
			}
		}

		static Program(){
			bg_thread=new System.Threading.Thread(bg_work);
			bg_thread.IsBackground=true;
			bg_thread.Name="<mwg::Sshfs::Program::Background>";
			bg_thread.Priority=System.Threading.ThreadPriority.BelowNormal;
			bg_thread.Start();
		}

		//==========================================================================
		public static readonly TimeoutExecutor ExecTimeout=new TimeoutExecutor();

		//**************************************************************************
		public class ArgumentReader{
			public ArgumentReader(){}

			public bool Read(string[] args){
				bool success=true;

				string argerr=null;
				for(int i=1;i<args.Length;i++){
					string arg=args[i];
					if(arg.Length==0)continue;
					if(arg.Length>=2&&(arg[0]=='-'||arg[0]=='/')){
						string name=arg.Substring(2);
						if(arg[1]=='-'){
							// --name オプションの処理
							/* 今の所、そう言うオプションはない */
							switch(name){
								case "help":
									this.printUsage();
									break;
							}
						}else switch(arg[1]){
							//----------------------------------------------------------------
							// -d: ドライブレターの指定
							//----------------------------------------------------------------
							case 'd': 
								if(name.Length==0){
									argerr="-d に続けてマウント先のドライブレターを指定して下さい。例: -dQ";
									goto argument_error;
								}
								if(!char.IsLetter(name[0])){
									argerr="ドライブレターとして指定できるのは、一文字の英数字です。例: -dQ";
									goto argument_error;
								}
								this.DriveLetter=name[0];
								break;
						}
					}else{

					}
				argument_error:
					System.Console.WriteLine("mwg.Sshfs! 引数 {0} は認識できません。",arg);
					System.Console.WriteLine("mwg.Sshfs! {0}",argerr);
					argerr=null;
					success=false;
				}

				return success;
			}
			//------------------------------------------------------------------------
			//	ドライブレター指定
			//------------------------------------------------------------------------
			private char driveLetter='R';
			public char DriveLetter{
				get{return this.driveLetter;}
				set{
					if(!char.IsLetter(value))return;
					this.driveLetter=value;
				}
			}
			//------------------------------------------------------------------------
			//	ヘルプ表示
			//------------------------------------------------------------------------
			private bool ishelp=false;
			public bool IsHelp{
				get{return this.ishelp;}
			}
			private void printUsage(){
				this.ishelp=true;
				System.Console.WriteLine(
@"usage: mwg.Sshfs [options]

Options
  -d<letter>     : letter にマウント先のドライブレターを指定します。
  --help         : この説明を表示します。
");
			}
		}

		internal static ArgumentReader arguments=new ArgumentReader();

		[System.STAThread]
		private static void Main(){
      //System.Console.WriteLine("Hello!");
      //System.Console.Out.WriteLine("World!");
#if add_accounts
			add_accounts();
			return;
#endif
			
      //mwg.Sshfs.Program.ssh_test2();
      //return;

			if(!arguments.Read(Environment.GetCommandLineArgs())){
				System.Console.Error.WriteLine("mwg.Sshfs! 引数指定に誤りが含まれていました。終了します。");
				System.Console.Error.WriteLine("mwg.Sshfs: 引数指定の詳細については mwg.Sshfs --help を見て下さい。");
				return;
			}

			if(arguments.IsHelp)return;

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			//SettingForm form=new SettingForm();
			//form.sftpAccountEditor1.Account=setting.GetAccount("kocoa") as SftpAccount;

			UserInterface.WndSetting form=new UserInterface.WndSetting();
			Application.Run(form);
		}

		public static void ssh_test1(){
			SshUserData data1=new SshUserData();
			data1.user="murase";
			data1.host="tkynt2.phys.s.u-tokyo.ac.jp";
			data1.port=22;
			data1.pass="";

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

			jsch::ChannelExec ch_e=(jsch::ChannelExec)sess1.openChannel("exec");
			ch_e.setCommand("cat");
			ch_e.setOutputStream(System.Console.OpenStandardOutput(),true);
      Tamir.SharpSsh.java.io.InputStream ins=ch_e.getInputStream();
			ch_e.connect();

			System.Console.WriteLine("ls -al ~/");
      System.IO.StreamWriter sw=new System.IO.StreamWriter(ins);
      sw.WriteLine("hello");

      //System.Threading.Thread.Sleep(2000);
      //System.Console.WriteLine("comp.");

      sw.Close();
      ins.close();

			ch_e.disconnect();
			sess1.disconnect();
		}

		public static void ssh_test2(){
			SshUserData data1=new SshUserData();
			data1.user="murase";
			data1.host="tkynt2.phys.s.u-tokyo.ac.jp";
			data1.port=22;
			data1.pass="";

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

      //MyProx proxy=new MyProx(sess1);

      //SshUserData data2=new SshUserData();
      //data2.user="murase";
      //data2.host="127.0.0.1";
      //data2.port=50022;
      //data2.pass="";
      //jsch::Session sess2=jsch.getSession(data2.user,data2.host,data2.port);
      //sess2.setConfig(config);
      //sess2.setUserInfo(new mwg.Sshfs.SshLoginInfo(mess1,data2));
      //sess2.setPassword(data2.pass);
      //sess2.setProxy(proxy);
      //sess2.connect();

      System.Console.WriteLine("cat");
      jsch::ChannelExec ch_e=(jsch::ChannelExec)sess1.openChannel("exec");
      ch_e.setCommand("cat");
      ch_e.setOutputStream(System.Console.OpenStandardOutput(),true);
      System.IO.Stream ins=ch_e.getOutputStream();
      ch_e.connect();


      System.Threading.Thread.Sleep(2000);
      System.Console.WriteLine("hello");
      ins.WriteByte((byte)'h');
      ins.WriteByte((byte)'e');
      ins.WriteByte((byte)'l');
      ins.WriteByte((byte)'l');
      ins.WriteByte((byte)'o');
      ins.WriteByte((byte)'\n');
      ins.Flush();
      //System.Threading.Thread.Sleep(2000);

      System.Threading.Thread.Sleep(2000);
      System.IO.StreamWriter sw=new System.IO.StreamWriter(ins);
      System.Console.WriteLine("test");sw.WriteLine("test");sw.Flush();
      System.Threading.Thread.Sleep(2000);
      System.Console.WriteLine("world");sw.WriteLine("world");sw.Flush();
      System.Threading.Thread.Sleep(2000);
      for(int i=0;i<5;i++){
        System.Console.WriteLine("count={0}",i);
        sw.WriteLine("count={0}",i);
        sw.Flush();
        System.Threading.Thread.Sleep(2000);
      }
      for(int i=5;i<20;i++){
        System.Console.WriteLine("count={0}",i);
        sw.WriteLine("count={0}",i);
        sw.Flush();
      }
      System.Threading.Thread.Sleep(2000);
      sw.Close();

      ins.Close();
			System.Console.WriteLine("comp.");

			ch_e.disconnect();
			//sess2.disconnect();
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


	}
}

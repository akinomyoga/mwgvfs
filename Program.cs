
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
			private char driveLetter='Q';
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
			
      //mwg.Sshfs.SharpSSHTest.ssh_test2();
      //mwg.Sshfs.SshNetTest.ssh_test();
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
	}
}

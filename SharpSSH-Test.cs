using jsch=Tamir.SharpSsh.jsch;

namespace mwg.Sshfs{
  class SharpSSHTest{
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

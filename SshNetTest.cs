#if USE_SSHNET
using Renci.SshNet;
using crypto=Renci.SshNet.Security.Cryptography;
using Renci.SshNet.Security.Cryptography.Ciphers;
using Gen=System.Collections.Generic;

namespace mwg.Sshfs{
  class SshNetTest{
		public static void ssh_test(){
			SshUserData data1=new SshUserData();
			data1.user="murase";
			//data1.host="tkynt2.phys.s.u-tokyo.ac.jp";
			data1.host="192.168.0.104";
			data1.port=50422;
			data1.pass="";
      data1.useIdentityFile=true;
      data1.idtt=@"C:\usr\cygwin\home\koichi\.ssh\id_rsa-padparadscha@gauge";
      data1.psph="";
      ssh_test1(data1);
    }

    static ConnectionInfo CreateConnectionInfo(SshUserData data){
      // create Authentication methods
      Gen::List<AuthenticationMethod> auth=new Gen::List<AuthenticationMethod>();
      if(data.pass!=null&&data.pass!=""){
        // Password based Authentication
        auth.Add(new PasswordAuthenticationMethod(data.user,data.pass));
      }
      if(data.useIdentityFile&&data.idtt!=""){
        // Key Based Authentication (using keys in OpenSSH Format)
        auth.Add(new PrivateKeyAuthenticationMethod(data.user,new PrivateKeyFile[]{ 
            new PrivateKeyFile(data.idtt,data.psph)
        }));
      }
      return new ConnectionInfo(data.host,data.port,data.user,auth.ToArray());
    }

    // 参考: https://gist.github.com/piccaso/d963331dcbf20611b094 わかりやすい
		public static void ssh_test1(SshUserData data1){
      ConnectionInfo info = CreateConnectionInfo(data1);
      using (var sshclient = new SshClient(info)){
        sshclient.Connect();

        //using(var cmd = sshclient.CreateCommand("mkdir -p /tmp/uploadtest && chmod +rw /tmp/uploadtest")){
        using(var cmd = sshclient.CreateCommand("(exit 12)")){
          cmd.Execute();
          System.Console.WriteLine("Command>" + cmd.CommandText);
          System.Console.WriteLine("Return Value = {0}", cmd.ExitStatus);
        }

        sshclient.Disconnect();
      }

      System.Console.WriteLine("done");
    }

    public static void ssh_test2(SshUserData data1){
      ConnectionInfo info = CreateConnectionInfo(data1);
      using (var ssh = new SshClient(info)){
        ssh.Connect();

        using(ShellStream stream = ssh.CreateShellStream("dumb",80,24,800,600,1024)){
          var writer = new System.IO.StreamWriter(stream);
          var reader = new System.IO.StreamReader(stream);
          writer.WriteLine("ls -la ~/");
          writer.WriteLine("echo;echo MWGVFS_EOF");
          writer.Flush();

          for(;;){
            string line;
            if(stream.Length==0)
              System.Threading.Thread.Sleep(50);
            else if((line = reader.ReadLine())!=null){
              if(line=="MWGVFS_EOF")break;
              System.Console.WriteLine(line);
            }
          }
        }

        ssh.Disconnect();
      }

      System.Console.WriteLine("done");
    }

    //Renci.SshNet.Security.Cryptography.Ciphers.AesCipher;

    public static void test1(){
      //var cinfo = new CipherInfo(256, (key, iv) => new Ciphers.AesCipher(key, new CtrCipherMode(iv), null));
      var cinfo = new CipherInfo(256, delegate(byte[] key, byte[] iv){
        return new crypto::Ciphers.AesCipher(key, new crypto::Ciphers.Modes.CtrCipherMode(iv), null);
      });

    }
  }
}
#endif

using Gen=System.Collections.Generic;
using Forms=System.Windows.Forms;
using Ser=System.Runtime.Serialization;

namespace mwg.Mounter{
	public interface IFsAccount{
		string Name{get;}
		IAccountEditor CreateEditorInstance();
	}
	public interface IAccountEditor{
		bool TrySetAccount(IFsAccount acc);
	}

	class ProgramSetting{
		internal Gen::List<IFsAccount> accounts;

		public void AddAccount(IFsAccount account){
			this.accounts.Add(account);
		}
		internal IFsAccount GetAccount(string name){
			foreach(IFsAccount account in accounts)
				if(account.Name==name)return account;
			return null;
		}

		public ProgramSetting(){
			this.accounts=new System.Collections.Generic.List<IFsAccount>();
		}
		//==========================================================================
		//	設定の保存と読み込み
		//==========================================================================
		static string CFGPATH_LIST=System.IO.Path.Combine(
			System.IO.Path.GetDirectoryName(Forms::Application.UserAppDataPath),
			"mwg_sshfs_cfg"
			);
		public static bool Save(ProgramSetting setting){
			try{
				System.Runtime.Serialization.Formatters.Binary.BinaryFormatter ser
				  =new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
				using(System.IO.FileStream stream=new System.IO.FileStream(CFGPATH_LIST,System.IO.FileMode.Create))
				using(
					System.IO.Compression.DeflateStream gzstr=new System.IO.Compression.DeflateStream(
					stream,System.IO.Compression.CompressionMode.Compress,true)
				){
					ser.Serialize(gzstr,new mwg.Mounter.Serialization.ProgramSettingData001(setting));
				}
				return true;
			}catch(System.Exception e){
				System.Console.WriteLine("mwg.Sshfs! 設定の保存に失敗しました");
				System.Console.WriteLine(e.ToString());
				return false;
			}
		}
		public static ProgramSetting Load(){
			try{
				System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bif
					=new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
				using(System.IO.FileStream stream=new System.IO.FileStream(CFGPATH_LIST,System.IO.FileMode.OpenOrCreate))
				using(
					System.IO.Compression.DeflateStream gzstr=new System.IO.Compression.DeflateStream(
					stream,System.IO.Compression.CompressionMode.Decompress,true)
				){
					mwg.Mounter.Serialization.ISaveData<ProgramSetting> data;
					data=(mwg.Mounter.Serialization.ISaveData<ProgramSetting>)bif.Deserialize(gzstr);
					return data.GenerateInstance();
				}
			}catch(System.Exception e){
				System.Console.WriteLine("mwg.Sshfs! 設定ファイルを読み込み中に例外が発生しました。");
				System.Console.WriteLine(e.ToString());
				return new ProgramSetting();
			}
		}
	}


}
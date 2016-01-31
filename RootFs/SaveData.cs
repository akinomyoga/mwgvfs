using Gen=System.Collections.Generic;
using Ser=System.Runtime.Serialization;

namespace mwg.Mounter.Serialization{
	internal class SerializationInfoReader{
		Ser::SerializationInfo info;
		public SerializationInfoReader(Ser::SerializationInfo info){
			this.info=info;
		}
		public bool GetValue<T>(string name,out T value,T defaultValue){
			try{
				value=(T)info.GetValue(name,typeof(T));
				return true;
			}catch{
				value=defaultValue;
				return false;
			}
		}
		public bool GetValue<T>(string name,out T value){
      return GetValue<T>(name,out value,default(T));
		}
    //public bool GetValueEnum<T>(string name,out T value){
    //  int enumValue;
    //  if(!this.GetValue(name,out enumValue)){
    //    value=default(T);
    //    return false;
    //  }

    //  value=(T)System.Enum.ToObject(typeof(T),enumValue);
    //}
	}

	interface ISaveData<T>{
		T GenerateInstance();
	}

	//============================================================================
	//	ProgramSetting 保存形式
	//----------------------------------------------------------------------------
	[System.Serializable]
	class ProgramSettingData000:ISaveData<ProgramSetting>{
		internal Gen::List<mwg.Sshfs.ISftpAccount> accounts=null;

		#region SaveData<ProgramSetting> メンバ
		public ProgramSettingData000(ProgramSetting setting){
			//this.accounts=setting.accounts;
			throw new System.InvalidOperationException("この保存形式は古い形式です。");
		}
		public ProgramSetting GenerateInstance(){
			//ProgramSetting setting=new ProgramSetting();
			//setting.accounts=this.accounts;
			//return setting;
			return new ProgramSettingData001(this).GenerateInstance();
		}
		#endregion
	}

	[System.Serializable]
	class ProgramSettingData001:ISaveData<ProgramSetting>{
		internal Gen::List<IFsAccount> accounts;

		#region SaveData<ProgramSetting> メンバ
		public ProgramSettingData001(ProgramSettingData000 data){
			this.accounts=new Gen::List<IFsAccount>();
			foreach(IFsAccount acc in data.accounts)
				this.accounts.Add(acc);
		}
		public ProgramSettingData001(ProgramSetting setting){
			this.accounts=setting.accounts;
		}
		public ProgramSetting GenerateInstance(){
			ProgramSetting setting=new ProgramSetting();
			setting.accounts=this.accounts;
			return setting;
		}
		#endregion
	}
}
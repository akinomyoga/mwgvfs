using Dokan;
using System.IO;
using jsch=Tamir.SharpSsh.jsch;
using java=Tamir.SharpSsh.java;
using Gen=System.Collections.Generic;

namespace mwg.Sshfs{
	class SftpStatCache{
		static System.TimeSpan TIMEOUT=new System.TimeSpan(0,0,5);
		struct lsres_t{
			public Gen::List<SftpFileInfo> result;
			public System.DateTime date; // 取得日時
			public lsres_t(Gen::List<SftpFileInfo> result,System.DateTime date){
				this.result=result;
				this.date=date;
			}
		}

		private SftpFsOperation operation;
		private object sync_root=new object();
		Gen::Dictionary<string,SftpFileInfo> dicattr=new Gen::Dictionary<string,SftpFileInfo>();
		Gen::Dictionary<string,lsres_t> diclist=new Gen::Dictionary<string,lsres_t>();
		System.DateTime dt_discard=System.DateTime.Now;
		public SftpStatCache(SftpFsOperation operation){
			this.operation=operation;
		}

		//--------------------------------------------------------------------------
		public bool TryGetAttr(string path,out jsch::SftpATTRS attr){
			this.DiscardOld();

			SftpFileInfo value;
			if(dicattr.TryGetValue(path,out value)){
				attr=value.Attribute;
				return true;
			}else{
				attr=null;
				return false;
			}
		}
		public void SetAttr(string path,jsch::SftpATTRS attr){
			this.InternalSetAttr(path,attr);
			this.DiscardOld();
		}
		void InternalSetAttr(string path,jsch::SftpATTRS attr){
			lock(sync_root)dicattr[path]=new SftpFileInfo(operation,path,attr);
		}
		void InternalSetAttr(SftpFileInfo attr){
			lock(sync_root)dicattr[attr.Filepath]=attr;
		}
		public void ClearFile(string path){
			lock(sync_root)dicattr.Remove(path);
		}
		//--------------------------------------------------------------------------
		public bool TryGetList(string path,out Gen::List<SftpFileInfo> result){
			this.DiscardOld();

			lsres_t value;
			if(diclist.TryGetValue(path,out value)){
				result=value.result;
				return true;
			}else{
				result=null;
				return false;
			}
		}
		public bool ClearList(string path){
			return diclist.Remove(path);
		}
		public Gen::List<SftpFileInfo> SetList(string path,java::util.Vector entries){
			string dir=path;
			if(!dir.EndsWith("/"))dir+="/";

			System.DateTime dt=System.DateTime.Now;
			Gen::List<SftpFileInfo> list=new Gen::List<SftpFileInfo>();

			foreach(jsch::ChannelSftp.LsEntry entry in entries){
				string filename=entry.getFilename();
				if(filename=="."){
					string filepath=path;
					SftpFileInfo item=new SftpFileInfo(operation,filepath,entry.getAttrs(),dt);
					this.InternalSetAttr(item);
				}else if(filename==".."){
					continue;
				}else{
					string filepath=dir+filename;
					SftpFileInfo item=new SftpFileInfo(operation,filepath,entry.getAttrs(),dt);
					list.Add(item);
					this.InternalSetAttr(item);
				}
			}
			diclist[path]=new lsres_t(list,dt);

			this.DiscardOld();
			return list;
		}
		//--------------------------------------------------------------------------
		private void DiscardOld(){
			System.DateTime dthresh=System.DateTime.Now-TIMEOUT;
			if(this.dt_discard>dthresh)return;

			lock(sync_root){
				Gen::List<string> remove_key=new Gen::List<string>();
				foreach(Gen::KeyValuePair<string,SftpFileInfo> pair in this.dicattr){
					if(pair.Value.infodate<dthresh)remove_key.Add(pair.Key);
				}
				foreach(string k in remove_key)this.dicattr.Remove(k);

				remove_key.Clear();
				foreach(Gen::KeyValuePair<string,lsres_t> pair in this.diclist){
					if(pair.Value.date<dthresh)remove_key.Add(pair.Key);
				}
				foreach(string k in remove_key)this.diclist.Remove(k);
			}
		}
	}


}
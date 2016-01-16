namespace mwg{

	/// <summary>
	/// 「或る処理を実行するのに一定時間以上掛かった場合に中断する」という操作を実装します。
	/// </summary>
	public class TimeoutExecutor:System.IDisposable{
		System.Threading.Thread timeout_thread;
		object timeout_sync=new object();
		System.Threading.ThreadStart timeout_proc;

		System.Exception err=null;

		public TimeoutExecutor(){
			timeout_thread=new System.Threading.Thread(timeout_work);
			timeout_thread.IsBackground=true;
			timeout_thread.Name="<mwg::TimeoutExucutor>";
			timeout_thread.Start();
		}

		private static object TERMINATE_THREAD=new object();
		public void Dispose(){
			timeout_thread.Abort(TERMINATE_THREAD);
		}

		void timeout_work(){
		begin:
			try{
				while(true){
					if(timeout_proc!=null){
						timeout_proc();
						timeout_proc=null;
					}
					System.Threading.Thread.Sleep(50);
				}
			}catch(System.Threading.ThreadAbortException e1){
				if(e1.ExceptionState==TERMINATE_THREAD)
					return; // スレッド終了

				timeout_proc=null;
				goto begin;
			}catch(System.Exception e2){
				timeout_proc=null;
				err=e2;
				goto begin;
			}
		}
		public bool Execute(System.Threading.ThreadStart proc,int milliseconds){
			lock(timeout_sync){
				timeout_proc=proc;
				err=null;
				
				// 待機
				long tick_end=System.DateTime.Now.Ticks+milliseconds*System.TimeSpan.TicksPerMillisecond;
				while(timeout_proc!=null&&System.DateTime.Now.Ticks<tick_end)
					System.Threading.Thread.Sleep(10);

				// 終了
				bool completed=timeout_proc==null;
				if(!completed){
					timeout_thread.Abort();
					timeout_proc=null;
				}
				if(err!=null)
					throw err;

				return completed;
			}
		}
	}

  //===========================================================================
  //  ReaderWriterLock を便利に使う為のクラス
  //---------------------------------------------------------------------------
  //  // 読取ロックで処理をする時
  //  using(new ReaderLock(rwlock)){
  //    処理...
  //  }
  //  // 書込ロックで処理をする時
  //  using(new WriterLock(rwlock)){
  //    処理...
  //  }
  //---------------------------------------------------------------------------
  public struct ReaderLock:System.IDisposable{
    System.Threading.ReaderWriterLock rwlock;
    public ReaderLock(System.Threading.ReaderWriterLock rwlock,int milliSeconds){
      this.rwlock=rwlock;
      this.rwlock.AcquireReaderLock(milliSeconds);
    }
    public ReaderLock(System.Threading.ReaderWriterLock rwlock)
      :this(rwlock,System.Threading.Timeout.Infinite){}

    public WriterLockUpgraded UpgradeToWriterLock(){
      return new WriterLockUpgraded(this.rwlock);
    }
    public WriterLockUpgraded UpgradeToWriterLock(int milliSeconds){
      return new WriterLockUpgraded(this.rwlock,milliSeconds);
    }

    public void Dispose(){
      if(this.rwlock!=null){
        this.rwlock.ReleaseReaderLock();
        this.rwlock=null;
      }
    }
  }
  public struct WriterLock:System.IDisposable{
    System.Threading.ReaderWriterLock rwlock;
    public WriterLock(System.Threading.ReaderWriterLock rwlock,int milliSeconds){
      this.rwlock=rwlock;
      this.rwlock.AcquireWriterLock(milliSeconds);
    }
    public WriterLock(System.Threading.ReaderWriterLock rwlock)
      :this(rwlock,System.Threading.Timeout.Infinite){}
    public void Dispose(){
      if(this.rwlock!=null){
        this.rwlock.ReleaseWriterLock();
        this.rwlock=null;
      }
    }
  }
  public struct WriterLockUpgraded:System.IDisposable{
    System.Threading.ReaderWriterLock rwlock;
    System.Threading.LockCookie cookie;
    internal WriterLockUpgraded(System.Threading.ReaderWriterLock rwlock,int milliSeconds){
      this.cookie=rwlock.UpgradeToWriterLock(milliSeconds);
      this.rwlock=rwlock;
    }
    internal WriterLockUpgraded(System.Threading.ReaderWriterLock rwlock)
      :this(rwlock,System.Threading.Timeout.Infinite){}
    public void Dispose(){
      if(this.rwlock!=null){
        this.rwlock.DowngradeFromWriterLock(ref this.cookie);
        this.rwlock=null;
      }
    }
  }

  //===========================================================================
  static class WinErrorCode{
    // msdn - Windows System Error Codes (http://msdn.microsoft.com/en-us/library/ms681381(v=VS.85).aspx)

    public const int ERROR_FILE_NOT_FOUND =2;
    public const int ERROR_PATH_NOT_FOUND =3;
    public const int ERROR_ACCESS_DENIED  =5;
    public const int ERROR_NOT_SAME_DEVICE=17;
    public const int ERROR_WRITE_PROTECT  =19;
    //public const int ERROR_FILE_EXISTS    =80;
    public const int ERROR_DIR_NOT_EMPTY  =145;
    public const int ERROR_DISK_FULL      =112;
    public const int ERROR_INVALID_NAME   =123;
    public const int ERROR_ALREADY_EXISTS =183;
    public const int ERROR_DIRECTORY      =267;
    public const int ERROR_FILE_READ_ONLY =6009; // 0x1779


    public const int ERROR_CANCELLED=1223;

    public const int ERROR_DIRECTORY_NOT_SUPPORTED=336;
  }
}
namespace mwg.Stream{
	interface IReader{
		System.IO.Stream SourceStream{set;}
	}
	interface IWriter{
		System.IO.Stream TargetStream{set;}
	}
	//============================================================================
	/// <summary>
	/// 他のストリームから読み取って他のストリームに書き込むクラスです。
	/// (==)
	/// </summary>
	class ConnectorAIAO{
		System.IO.Stream istr;
		System.IO.Stream ostr;
		int bufferSize;
		bool started=false;
		//--------------------------------------------------------------------------
		//	初期化
		//--------------------------------------------------------------------------
		public ConnectorAIAO(System.IO.Stream istr,System.IO.Stream ostr)
			:this(istr,ostr,0x1000){}
		public ConnectorAIAO(System.IO.Stream istr,System.IO.Stream ostr,int bufferSize){
			if(!istr.CanRead)throw new System.ArgumentException("指定された実引数は読取用ストリームではありません。","istr");
			if(!istr.CanRead)throw new System.ArgumentException("指定された実引数は書込用ストリームではありません。","ostr");
			this.istr=istr;
			this.ostr=ostr;
			this.bufferSize=bufferSize;
		}
		//--------------------------------------------------------------------------
		//	動作
		//--------------------------------------------------------------------------
		public void Connect(){
			if(started)return;
			started=true;

			int SZBUFF=bufferSize;
			byte[] buff=new byte[SZBUFF];
			while(true){
				int c=this.istr.Read(buff,0,SZBUFF);
				if(c==0)return;
				this.ostr.Write(buff,0,c);
			}
		}
		public System.Threading.Thread BeginConnect(bool background){
			System.Threading.Thread thread=new System.Threading.Thread(this.Connect);
			thread.IsBackground=background;
			thread.Name="<mwg.Stream.ConnectorAIAO> "+istr.ToString()+" -> "+ostr.ToString();
			thread.Start();
			return thread;
		}
		//--------------------------------------------------------------------------
		//	データ
		//--------------------------------------------------------------------------
		System.IO.Stream TargetStream{
			get{return this.ostr;}
		}
		System.IO.Stream SourceStream{
			get{return this.istr;}
		}
	}
	//============================================================================
	/// <summary>
	/// Write によって書込を受けて、Read によって読取を行います。
	/// )==(
	/// </summary>
	class ConnectorPIPO{
		System.IO.Stream OutputStream{
			get{return null;}
		}
		System.IO.Stream InputStream{
			get{return null;}
		}
	}
}
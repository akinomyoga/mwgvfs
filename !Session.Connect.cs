using System;
using System.IO;

public class Session{

	public void connect_1(){
		while(true){
			int i=0;
			int nread=0;
			while(i<this.buf.buffer.Length){
				nread=this.io.getByte();
				if(nread<0)break;

				this.buf.buffer[i++]=(byte)nread;
				if(nread==10)break;
			}
			if(nread<0)
				throw new JSchException("connection is closed by foreign host");

			if(this.buf.buffer[i-1]=='\n'){
				i--;
				if(this.buf.buffer[i-1]=='\r'){
					i--;
				}
			}
			if(i<=4||i==this.buf.buffer.Length||this.buf.buffer[0]=='S'&&this.buf.buffer[1]=='S'&&this.buf.buffer[2]=='H'&&this.buf.buffer[3]=='-'){
				if(i==this.buf.buffer.Length||i<7||this.buf.buffer[4]=='1'&&this.buf.buffer[6]!='9'){
					throw new JSchException("invalid server's version String");
				}
				return i;
			}
		}
	}
	public void connect(int connectTimeout){
		Exception exception;
		Exception exception6;
		if (this._isConnected){
			throw new JSchException("session is already connected");
		}
		this.io=new IO();
		if (random==null){
			try{
				random=(Random) Class.forName((string) this.getConfig("random")).newInstance();
			}catch (Exception exception1){
				exception=exception1;
				Console.Error.WriteLine("connect: random "+exception);
			}
		}

		Packet.setRandom(random);

		try{
			int i;
			UserAuth auth;

			Proxy proxy;
			if(this.proxy==null){
				this.proxy=this.jsch.getProxy((string)this.host);
				if(this.proxy!=null)
					lock(proxy=this.proxy)
						this.proxy.close();
			}

			if(this.proxy==null){
				Stream stream;
				Stream stream2;
				if(this.socket_factory==null){
					this.socket=Util.createSocket((string)this.host,this.port,connectTimeout);
					stream=this.socket.getInputStream();
					stream2=this.socket.getOutputStream();
				}else{
					this.socket=this.socket_factory.createSocket((string)this.host, this.port);
					stream=this.socket_factory.getInputStream(this.socket);
					stream2=this.socket_factory.getOutputStream(this.socket);
				}
				this.socket.setTcpNoDelay(true);
				this.io.setInputStream(stream);
				this.io.setOutputStream(stream2);
			}else{
				lock(proxy=this.proxy){
					this.proxy.connect(this.socket_factory,this.host,this.port,connectTimeout);
					this.io.setInputStream(this.proxy.getInputStream());
					this.io.setOutputStream(this.proxy.getOutputStream());
					this.socket=this.proxy.getSocket();
				}
			}
			if(connectTimeout>0&&this.socket!=null){
				this.socket.setSoTimeout(connectTimeout);
			}
			this._isConnected=true;

			int i=this.connect_1();

			this.V_S=new byte[i];
			System.arraycopy(this.buf.buffer, 0L, this.V_S, 0L, (long) i);
			byte[] buffer=new byte[this.V_C.Length+1];
			System.arraycopy(this.V_C, 0L, buffer, 0L, (long) this.V_C.Length);
			buffer[buffer.Length - 1]=10;
			this.io.put(buffer, 0, buffer.Length);
			this.buf=this.read(this.buf);
			if(this.buf.buffer[5]!=20){
				throw new JSchException("invalid protocol: "+this.buf.buffer[5]);
			}

			KeyExchange kex=this.receive_kexinit(this.buf);
			while(true){
				this.buf=this.read(this.buf);
				if(kex.getState()==this.buf.buffer[5]){
					bool flag=kex.next(this.buf);
					if (!flag){
						this.in_kex=false;
						throw new JSchException("verify: "+flag);
					}
				}else{
					this.in_kex=false;
					throw new JSchException("invalid protocol(kex): "+this.buf.buffer[5]);
				}
				if(kex.getState()==0)break;
			}

			try{
				this.checkHost(this.host, kex);
			}catch (JSchException exception2){
				this.in_kex=false;
				throw exception2;
			}
			this.send_newkeys();
			this.buf=this.read(this.buf);
			if (this.buf.buffer[5]==0x15){
				this.receive_newkeys(this.buf, kex);
			}else{
				this.in_kex=false;
				throw new JSchException("invalid protocol(newkyes): "+this.buf.buffer[5]);
			}
			bool flag2=false;
			bool flag3=false;
			UserAuthNone none=new UserAuthNone(this.userinfo);
			String str=null;
			flag2=none.start(this);
			if(!flag2){
				str=none.getMethods();
				if(str!=null){
					str=str.toLowerCase();
				}else{
					str="publickey,password,keyboard-interactive";
				}
			}

			while(!flag2&&str!=null&&str.Length()>0){
				auth=null;
				if(str.startsWith("publickey")){
					lock(this.jsch.identities){
						if(this.jsch.identities.size()>0){
							auth=new UserAuthPublicKey(this.userinfo);
						}
					}
				}else if(str.startsWith("keyboard-interactive")){
					if(this.userinfo is UIKeyboardInteractive){
						auth=new UserAuthKeyboardInteractive(this.userinfo);
					}
				}else if(str.startsWith("password")){
					auth=new UserAuthPassword(this.userinfo);
				}
				if(auth!=null){
					try{
						flag2=auth.start(this);
						flag3=false;
					}catch(JSchAuthCancelException){
						flag3=true;
					}catch(JSchPartialAuthException exception4){
						str=exception4.getMethods();
						flag3=false;
						continue;
					}catch(RuntimeException exception5){
						throw exception5;
					}catch(Exception exception11){
						exception6=exception11;
						Console.WriteLine("ee: "+exception6);
					}
				}
				if(!flag2){
					int num4=str.indexOf(",");
					if(num4==-1)break;

					str=str.subString(num4+1);
				}
			}

			if(connectTimeout>0||this.timeout>0){
				this.socket.setSoTimeout(this.timeout);
			}

			if(flag2){
				this.isAuthed=true;
				this.connectThread=new Thread(this);
				this.connectThread.setName(("Connect thread "+this.host)+" session");
				this.connectThread.start();
			}else if(flag3){
				throw new JSchException("Auth cancel");
			}else{
				throw new JSchException("Auth fail");
			}
		}catch (Exception exception12){
			exception=exception12;
			this.in_kex=false;
			if(this._isConnected){
				try{
					this.packet.reset();
					this.buf.putByte((byte) 1);
					this.buf.putInt(3);
					this.buf.putString(new String(exception.ToString()).getBytes());
					this.buf.putString(new String("en").getBytes());
					this.write(this.packet);
					this.disconnect();
				}catch(Exception exception13){
					exception6=exception13;
				}
			}
			this._isConnected=false;
			if(exception is RuntimeException){
				throw ((RuntimeException) exception);
			}
			if(exception is JSchException){
				throw ((JSchException) exception);
			}
			throw new JSchException("Session.connect: "+exception);
		}
	}
}

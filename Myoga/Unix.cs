//#define ENABLE_QUOTED_WILDCARD
using Gen=System.Collections.Generic;

namespace mwg.Unix{
  public static class UnixPath{
    public static string Combine(string dir,string path){
      if(path.Length==0)return dir;
      if(dir.Length==0||path[0]=='/')return path;
      if(dir.EndsWith("/"))dir=dir.Substring(0,dir.Length-1);

      // . .. の処理
      while(true){
        if(path.StartsWith("../")){
          path=path.Substring(3);
          dir=GetDirectoryPath(dir);
        }else if(path.StartsWith("./")){
          path=path.Substring(2);
        }else break;
      }

      if(!dir.EndsWith("/"))dir+="/";
      return dir+path;
    }
    public static string GetDirectoryPath(string path){
      int index=path.LastIndexOf('/');
      if(index<0)return ".";
      if(index==0)return "/";
      return path.Substring(0,index);
    }
    public static string GetParentPath(string path){
      if(path.Length>0&&path[path.Length-1]=='/')
        path=path.Substring(0,path.Length-1);
      return GetDirectoryPath(path);
    }
    public static string GetFileName(string path){
      int index=path.LastIndexOf('/');
      if(index<0)return path;
      return path.Substring(index+1);
    }
    //=========================================================================
    //  パス規格化
    //-------------------------------------------------------------------------
    static Gen::List<string> InternalResolveTraversal(string path){
      Gen::List<string> list=new System.Collections.Generic.List<string>();
      string[] elems=path.Split('/');
      for(int index=0;index<elems.Length;index++){
        string elem=elems[index];
        if(index==0){
          list.Add(elem);
          continue;
        }

        if(elem.Length==0||elem=="."){
          continue;
        }else if(elem==".."){
          if(list.Count==1){
            if(list[0].Length==0)
              continue; // 絶対パス
            else if(list[0]==".")
              list.RemoveAt(0);
          }

          if(list.Count>0&&list[list.Count-1]!="..")
            list.RemoveAt(list.Count-1);
          else
            list.Add(elem);
        }else{
          list.Add(elem);
        }
      }

      return list;
    }
    static string ResolveTraversal(string path){
      Gen::List<string> list=InternalResolveTraversal(path);
      if(list.Count==1)
        return list[0].Length==0?"/":list[0];

      System.Text.StringBuilder build=new System.Text.StringBuilder();
      bool isfirst=true;
      foreach(string e in list){
        if(isfirst)
          isfirst=false;
        else
          build.Append('/');

        build.Append(e);
      }
      return build.ToString();
    }

    public static string GetRelativePathTo(string content,string root){
      // 0 canonical
      // 1 共通部分を削除
      // 2 遡って、削除
      Gen::List<string> listC=InternalResolveTraversal(content);
      Gen::List<string> listR=InternalResolveTraversal(root);

      bool isAbsC=listC.Count>0&&listC[0].Length==0;
      bool isAbsR=listR.Count>0&&listR[0].Length==0;
      if(isAbsC!=isAbsR)return content;

      int ihead=0;
      while(ihead<listC.Count&&ihead<listR.Count&&listC[ihead]==listR[ihead])ihead++;

      bool isfirst=true;
      System.Text.StringBuilder build=new System.Text.StringBuilder();
      for(int index=ihead;index<listR.Count;index++){
        if(isfirst)
          isfirst=false;
        else
          build.Append('/');

        build.Append("..");
      }

      for(int index=ihead;index<listC.Count;index++){
        if(isfirst)
          isfirst=false;
        else
          build.Append('/');

        build.Append(listC[index]);
      }
      
      return build.ToString();
    }

    public static string QuoteWildcard(string upath){
      System.Text.StringBuilder b=new System.Text.StringBuilder(upath.Length*2);
      foreach(char c in upath){
        if(c=='\\')
          continue;
        else if(c=='*'||c=='?')
          b.Append('\\');
        b.Append(c);
      }
      return b.ToString();
    }
    //=========================================================================
    //  Windows Path と Unix Path の相互変換
    //-------------------------------------------------------------------------
    static string ConvertUPathToWPath(string upath,int start,int end){
      System.Text.StringBuilder b=new System.Text.StringBuilder();
      for(int i=start;i<end;i++){
        char c=upath[i];
        if(c<'\x40'){
          if(c<'\x20'){
            if(c=='\0')
              break;
            else if('\0'<c&&c<='\x07'||'\x0A'<=c)
              goto cygwin_escape;
            else
              continue;
          }else{
            switch(c){
              case '"':case '*':case ':':
              case '<':case '>':case '?':
                goto cygwin_escape;
            }
          }
#if ENABLE_QUOTED_WILDCARD
        }else if(c=='\\'){
          continue;
#else
        }else if(c=='\\'){
          goto cygwin_escape;
#endif
        }else if(c=='|'){
          goto cygwin_escape;
        }
        
        b.Append(c);
        continue;
      cygwin_escape:
        b.Append((char)((int)c+0xF000));
        continue;
      }
      return b.ToString();
    }
    static string ConvertWPathToUPath(string wpath,int start,int end){
      System.Text.StringBuilder b=new System.Text.StringBuilder();
      for(int i=start;i<end;i++){
        char c=wpath[i];
        if('\xF000'<c&&c<'\xF080'){
          if(c<'\xF020'){
            if('\xF000'<c&&c<='\xF007'||'\xF00A'<=c)
              goto cygwin_unescape;
          }else if(c<'\xF040'){
#if ENABLE_QUOTED_WILDCARD
            if(c=='\xF022'||c=='\xF03A'||c=='\xF03C'||c=='\xF03E')
              goto cygwin_unescape;
            else if(c=='\xF02A'||c=='\xF03F')
              goto cygwin_unescape_bs;
          }else
            if(c=='\xF07C')
              goto cygwin_unescape;
#else
            if(c=='\xF022'||c=='\xF03A'||c=='\xF03C'||c=='\xF03E'||c=='\xF02A'||c=='\xF03F')
              goto cygwin_unescape;
          }else
            if(c=='\xF07C'||c=='\xF05C')
              goto cygwin_unescape;
#endif
        }else if(c=='\\'){
          b.Append('/');
          continue;
        }

        b.Append(c);
        continue;
#if ENABLE_QUOTED_WILDCARD
      cygwin_unescape_bs:
        b.Append('\\');
        goto cygwin_unescape;
#endif
      cygwin_unescape:
        b.Append((char)((int)c-0xF000));
        continue;
      }

      return b.ToString();
    }
    /// <summary>
    /// Windows のパス名から unix 形式のパス名に変換を行います。
    /// 1. パスの区切り文字 '\\' を '/' に変換します。
    /// 2. Cygwin 形式で escape された文字を復元します。
    ///    Cygwin では Windows のファイルシステムで扱えない特別な文字
    ///    (':','"','&lt;','&gt;','|','?','*' 等) を私用領域の文字を使って表現します。
    ///    この関数は、私用領域で表現された特別な文字を、本来の文字に変換します。
    /// </summary>
    /// <param name="lpath">Windows 形式のパス名を指定します。</param>
    /// <returns>変換後の unix 形式のパス名を返します。</returns>
    public static string ConvertWPathToUPath(string wpath){
      if(wpath.Length==0)return "";
      return ConvertWPathToUPath(wpath,0,wpath.Length);
    }
    public static string GetWFileName(string upath){
      int start=upath.LastIndexOf('/');
      if(start<0)
        start=0;
      else
        start++;

      return ConvertUPathToWPath(upath,start,upath.Length);
    }

  }

  public static class UnixTime{
    static System.DateTime UNIX_TIMEBASE=new System.DateTime(1970,1,1,0,0,0,System.DateTimeKind.Utc);

    public static System.DateTime UnixTimeToDateTime(int unixtime){
      return UNIX_TIMEBASE.AddSeconds(unixtime);
    }
    public static int DateTimeToUnixTime(System.DateTime datetime){
      System.TimeSpan span=datetime.ToUniversalTime()-UNIX_TIMEBASE;
      return (int)span.TotalSeconds;
    }
  }

  public enum SSH_ERROR{
    // from http://www.eldos.com/documentation/sbb/documentation/ref_err_ssherrorcodes.html
    WRONG_MODE              =-1 , //  (0xFFFFFFFF)  Attempt to call synchronous method in asynchronous mode and vice versa.
    OK                      =0  , //  (0x0000)      Indicates successful completion of the operation
    EOF                     =1  , //  (0x0001)      indicates end-of-file condition; Read: no more data is available in the file;
                                  //                # ReadDirectory: no more files are contained in the directory.
    NO_SUCH_FILE            =2  , //  (0x0002)      A reference is made to a file which does not exist.
    PERMISSION_DENIED       =3  , //  (0x0003)      the authenticated user does not have sufficient permissions to perform the operation.
    FAILURE                 =4  , //  (0x0004)      An error occurred for which there is no more specific error code defined.
    BAD_MESSAGE             =5  , //  (0x0005)      A badly formatted packet or protocol incompatibility is detected.
    NO_CONNECTION           =6  , //  (0x0006)      A pseudo-error which indicates that the client has no connection to the server.
    CONNECTION_LOST         =7  , //  (0x0007)      A pseudo-error which indicates that the connection to the server has been lost.
    OP_UNSUPPORTED          =8  , //  (0x0008)      An attempt was made to perform an operation which is not supported for the server.
    INVALID_HANDLE          =9  , //  (0x0009)      The handle value was invalid.
    NO_SUCH_PATH            =10 , //  (0x000A)      The file path does not exist or is invalid.
    FILE_ALREADY_EXISTS     =11 , //  (0x000B)      The file already exists.
    WRITE_PROTECT           =12 , //  (0x000C)      The file is on read only media, or the media is write protected.
    NO_MEDIA                =13 , //  (0x000D)      The requested operation can not be completed because there is no media available in the drive.
    NO_SPACE_ON_FILESYSTEM  =14 , //  (0x000E)      The requested operation cannot be completed because there is no free space on the filesystem.
    QUOTA_EXCEEDED          =15 , //  (0x000F)      The operation cannot be completed because it would exceed the user's storage quota.
    UNKNOWN_PRINCIPAL       =16 , //  (0x0010)      A principal referenced by the request (either the 'owner', 'group', or 'who' field of an ACL), was unknown.
    LOCK_CONFLICT           =17 , //  (0x0011)      The file could not be opened because it is locked by another process.
    DIR_NOT_EMPTY           =18 , //  (0x0012)      The directory is not empty.
    NOT_A_DIRECTORY         =19 , //  (0x0013)      The specified file is not a directory.
    INVALID_FILENAME        =20 , //  (0x0014)      The filename is not valid.
    LINK_LOOP               =21 , //  (0x0015)      Too many symbolic links encountered.
    CANNOT_DELETE           =22 , //  (0x0016)      The file cannot be deleted. One possible reason is that the advisory READONLY attribute-bit is set.
    INVALID_PARAMETER       =23 , //  (0x0017)      On of the parameters was out of range, or the parameters specified cannot be used together.
    FILE_IS_A_DIRECTORY     =24 , //  (0x0018)      The specified file was a directory in a context where a directory cannot be used.
    BYTE_RANGE_LOCK_CONFLICT=25 , //  (0x0019)      A read or write operation failed because another process's mandatory byte-range lock overlaps with the request.
    BYTE_RANGE_LOCK_REFUSED =26 , //  (0x001A)      A request for a byte range lock was refused.
    DELETE_PENDING          =27 , //  (0x001B)      An operation was attempted on a file for which a delete operation is pending.
    FILE_CORRUPT            =28 , //  (0x001C)      The file is corrupt; an filesystem integrity check should be run.
    OWNER_INVALID           =29 , //  (0x001D)      The principal specified can not be assigned as an owner of a file.
    GROUP_INVALID           =30 , //  (0x001E)      The principal specified can not be assigned as the primary group of a file.
    UNSUPPORTED_VERSION     =100, //  (0x0064)      Sets of supported by client and server versions has no intersection.
    INVALID_PACKET          =101, //  (0x0065)      Invalid packet was received.
    TUNNEL_ERROR            =102, //  (0x0066)      Error is on the SSH-protocol level. The connection is closed because of SSH error.
    CONNECTION_CLOSED       =103, //  (0x0067)      Connection is closed.
  }
}
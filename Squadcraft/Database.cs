using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Squadcraft {
    public static class Database {
        /*
         * 
         * jest hostowany, zwraca nazwe hostera i ip (ret 0)
           jest hostowany, bledny klucz (join key) (ret 1)
	       nie jest hostowany, bledny klucz (host key) (ret 2)
	       nie jest hostowany, poprawny klucz (ret 3)
	       jest hostowany i ty jestes hostującym -> bledny klucz (ret 4)
	       jest hostowany i ty jestes hostującym -> poprawny klucz -> update czasu (ret 5)
           serwer nie istnieje: ret 6
           sesja zle zamknieta: ret 7
           sesja zakonczona: ret 8
         * 
         * 
         */

        public enum Response { hostedReadyToJoin, hostedInvalidKey, invalidHostKey, readyToHost, hostUnexpectedError, hostUpdate, serverDoesNotExist, sessionNotClosedProperly, unexpectedError = -1,
            closedSuccessfully = 8
        }

        public static string GetResponse(out Response response, bool endSession = false) {
            response = Response.unexpectedError;
            Uri url = null;
            try {
                StringBuilder sb = new StringBuilder(Program.conf.hostStateResponseUrl + "?");
                sb.Append($"server_name={Program.conf.serverName}");
                sb.Append($"&git_username=" + Program.conf.gitUsername);
                sb.Append($"&secret_join_key={Program.conf.serverJoinSecretKey}");
                sb.Append($"&secret_host_key={Program.conf.serverHostSecretKey}");
                sb.Append("&ip=" + (endSession ? "_(TERMIN@TE)_" : Program.serverIp));
                url = new Uri(Uri.EscapeUriString(sb.ToString()));
                using (WebClient wc = new WebClient()) {
                    string s = wc.DownloadString(url);
                    response = (Response)int.Parse(s[0].ToString());
                    return s.Substring(1, s.Length - 1);
                }
            } catch (Exception e) {
                Program.ErrorExit($"Could not get host state from " + (url != null ? url.AbsolutePath : "") + $": {e.Message}", false);
                return null;
            }
        }

    }
}

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Trickshot
{
    [Serializable]
    public class MatchPlayer
    {
        public string playerId;
        public string name;
        public string team;
    }

    [Serializable]
    public class MatchInfo
    {
        public string matchId;
        public string mode;
        public string map;
        public string team;   // "red" or "blue": the local player's side
        public string server; // empty until game servers exist
        public MatchPlayer[] players;
    }

    /// <summary>
    /// NOT SWITCHED ON. Talks to the matchmaking server in Backend/matchmaking: joins the queue for
    /// a mode, polls the ticket, and raises MatchFound. Nothing adds this component to the scene yet
    /// and onlineEnabled is off, because there is no networked play to hand the match to.
    /// When there is: add it next to MainMenu, turn onlineEnabled on, call FindMatch("tdm") or
    /// FindMatch("elimination"), and start the networked match from MatchFound.
    /// </summary>
    public class MatchmakingClient : MonoBehaviour
    {
        [Tooltip("Leave off until online play exists.")]
        public bool onlineEnabled = false;
        public string serverUrl = "http://127.0.0.1:8787";
        public float pollSeconds = 1f;

        public bool Searching { get; private set; }
        /// <summary>Human-readable state for the menu, such as "Searching (position 3)".</summary>
        public string Status { get; private set; } = "Offline";

        public event Action<MatchInfo> MatchFound;
        public event Action<string> Failed;

        [Serializable]
        class QueueRequest
        {
            public string playerId;
            public string name;
            public string mode;
            public int skill;
        }

        [Serializable]
        class TicketState
        {
            public string ticketId;
            public string status;
            public int position;
            public MatchInfo match;
        }

        string ticketId;
        Coroutine search;

        /// <summary>A random id kept on this machine. A stand-in until there are real accounts.</summary>
        public static string PlayerId
        {
            get
            {
                string id = PlayerPrefs.GetString("Trickshot.PlayerId", "");
                if (id.Length == 0)
                {
                    id = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString("Trickshot.PlayerId", id);
                }
                return id;
            }
        }

        public void FindMatch(string mode, string playerName = "Player", int skill = 1000)
        {
            if (!onlineEnabled)
            {
                Fail("Online play is not switched on yet");
                return;
            }
            Cancel();
            search = StartCoroutine(Search(mode, playerName, skill));
        }

        public void Cancel()
        {
            if (search != null) StopCoroutine(search);
            search = null;
            Searching = false;
            if (!string.IsNullOrEmpty(ticketId)) StartCoroutine(Send("DELETE", "/v1/queue/" + ticketId, null, null));
            ticketId = null;
            Status = "Offline";
        }

        void OnDisable()
        {
            if (Searching) Cancel();
        }

        IEnumerator Search(string mode, string playerName, int skill)
        {
            Searching = true;
            Status = "Joining queue";

            var body = new QueueRequest { playerId = PlayerId, name = playerName, mode = mode, skill = skill };
            TicketState state = null;
            yield return Send("POST", "/v1/queue", JsonUtility.ToJson(body), text => state = JsonUtility.FromJson<TicketState>(text));
            if (state == null || string.IsNullOrEmpty(state.ticketId))
            {
                Fail("Could not reach the matchmaking server");
                yield break;
            }
            ticketId = state.ticketId;

            while (true)
            {
                if (state.status == "matched")
                {
                    Searching = false;
                    search = null;
                    ticketId = null;
                    Status = "Match found";
                    MatchFound?.Invoke(state.match);
                    yield break;
                }
                if (state.status != "searching")
                {
                    Fail("Left the queue (" + state.status + ")");
                    yield break;
                }

                Status = "Searching (position " + state.position + ")";
                yield return new WaitForSecondsRealtime(pollSeconds);

                TicketState next = null;
                yield return Send("GET", "/v1/queue/" + ticketId, null, text => next = JsonUtility.FromJson<TicketState>(text));
                if (next == null)
                {
                    Fail("Lost contact with the matchmaking server");
                    yield break;
                }
                state = next;
            }
        }

        void Fail(string reason)
        {
            Searching = false;
            search = null;
            ticketId = null;
            Status = reason;
            Failed?.Invoke(reason);
        }

        IEnumerator Send(string method, string path, string json, Action<string> onText)
        {
            using (var request = new UnityWebRequest(serverUrl.TrimEnd('/') + path, method))
            {
                if (json != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 5;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success && onText != null)
                    onText(request.downloadHandler.text);
            }
        }
    }
}

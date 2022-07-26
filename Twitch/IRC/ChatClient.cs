﻿using Quicksand.Web;
using Quicksand.Web.WebSocket;
using StreamFeedstock;
using StreamGlass.StreamChat;
using System;
using System.Collections.Generic;
using System.Runtime;
using System.Text.RegularExpressions;
using static StreamGlass.StreamChat.UserMessage;
using Client = Quicksand.Web.WebSocket.Client;

namespace StreamGlass.Twitch.IRC
{
    public class ChatClient: AClientListener
    {
        private static readonly List<string> ms_Colors = new()
        {
            "#ff0000",
            "#00ff00",
            "#0000ff",
            "#b22222",
            "#ff7f50",
            "#9acd32",
            "#ff4500",
            "#2e8b57",
            "#daa520",
            "#d2691e",
            "#5f9ea0",
            "#1e90ff",
            "#ff69b4",
            "#8a2be2",
            "#00ff7f"
        };

        private readonly Settings.Data m_Settings;
        private readonly Client m_Client;
        private UserInfo? m_SelfUserInfo = null;
        private string m_ChatColor = "";
        private string m_Channel = "";
        private OAuthToken? m_AccessToken = null;
        private string m_UserName = "";

        public ChatClient(Settings.Data settings)
        {
            m_Settings = settings;
            m_Client = new(this, "wss://irc-ws.chat.twitch.tv:443");
        }

        public void SetSelfUserInfo(UserInfo? info) => m_SelfUserInfo = info;

        public void Init(string username, OAuthToken token)
        {
            m_UserName = username;
            m_AccessToken = token;
            m_AccessToken.Refreshed += (_) => SendAuth();
            m_Client.Connect();
        }

        internal void SendAuth()
        {
            if (m_AccessToken == null)
                return;
            SendMessage(new("CAP REQ", parameters: "twitch.tv/membership twitch.tv/tags twitch.tv/commands"));
            SendMessage(new("PASS", channel: string.Format("oauth:{0}", m_AccessToken.Token)));
            SendMessage(new("NICK", channel: (m_SelfUserInfo != null) ? m_SelfUserInfo.DisplayName : m_UserName));
        }

        private string GetUserMessageColor(string username, string color)
        {
            if (string.IsNullOrEmpty(color))
            {
                int colorIdx = 0;
                foreach (char c in username)
                    colorIdx += c;
                colorIdx %= ms_Colors.Count;
                return ms_Colors[colorIdx];
            }
            return color;
        }

        private void CreateUserMessage(Message message, bool highlight, bool self)
        {
            string username;
            if (self)
            {
                if (m_SelfUserInfo != null)
                    username = m_SelfUserInfo.DisplayName;
                else
                    username = "StreamGlass";
            }
            else
            {
                username = message.GetTag("display-name");
                if (string.IsNullOrEmpty(username))
                    username = message.Nick;
            }
            UserType userType = UserType.NONE;
            if (self)
                userType = UserType.SELF;
            else
            {
                if (message.GetTag("mod") == "1")
                    userType = UserType.MOD;
                switch (message.GetTag("user-type"))
                {
                    case "admin": userType = UserType.ADMIN; break;
                    case "global_mod": userType = UserType.GLOBAL_MOD; break;
                    case "staff": userType = UserType.STAFF; break;
                }
                if (message.HaveBadge("broadcaster"))
                    userType = UserType.BROADCASTER;
            }
            DisplayableMessage displayableMessage = Helper.Convert(message.Parameters, message.Emotes);
            CanalManager.Emit(StreamGlassCanals.CHAT_MESSAGE, new UserMessage(highlight, message.GetTag("id"),
                username, GetUserMessageColor(username, (self) ? m_ChatColor : message.GetTag("color")),
                message.GetCommand().Channel, userType, displayableMessage));
            if (message.HaveTag("bits"))
            {
                int bits = int.Parse(message.GetTag("bits"));
                CanalManager.Emit(StreamGlassCanals.DONATION, new DonationEventArgs(username, bits!, "Bits", displayableMessage));
            }
        }

        private void TreatUserNotice(Message message)
        {
            if (m_Settings.Get("twitch", "sub_mode") == "all")
                return;
            CreateUserMessage(message, true, false);
            if (message.HaveTag("msg-id"))
            {
                string noticeType = message.GetTag("msg-id");
                if (noticeType == "sub" || noticeType == "resub")
                {
                    if (message.HaveTag("msg-param-sub-plan") &&
                        message.HaveTag("msg-param-cumulative-months") &&
                        message.HaveTag("msg-param-should-share-streak") &&
                        message.HaveTag("msg-param-streak-months"))
                    {
                        string username = message.GetTag("display-name");
                        if (string.IsNullOrEmpty(username))
                            username = message.Nick;
                        int followTier = 0;
                        switch (message.GetTag("msg-param-sub-plan"))
                        {
                            case "1000": followTier = 1; break;
                            case "2000": followTier = 2; break;
                            case "3000": followTier = 3; break;
                            case "Prime": followTier = 4; break;
                            default: return;
                        }
                        int cumulativeMonth = int.Parse(message.GetTag("msg-param-cumulative-months"));
                        bool shareStreakMonth = message.GetTag("msg-param-cumulative-months") == "1";
                        int streakMonth = int.Parse(message.GetTag("msg-param-streak-months"));
                        DisplayableMessage displayableMessage = Helper.Convert(message.Parameters, message.Emotes);
                        CanalManager.Emit(StreamGlassCanals.FOLLOW, new FollowEventArgs(username, displayableMessage, followTier, false, cumulativeMonth, (shareStreakMonth) ? streakMonth : -1, -1));
                    }
                }
            }
        }

        internal void TreatReceivedBuffer(string dataBuffer)
        {
            int position;
            do
            {
                position = dataBuffer.IndexOf("\r\n");
                if (position >= 0)
                {
                    string data = dataBuffer[..position];
                    Logger.Log("Twitch IRC", string.Format("<= {0}", data));
                    dataBuffer = dataBuffer[(position + 2)..];
                    Message? message = Message.Parse(data);
                    if (message != null)
                    {
                        switch (message.GetCommand().Name)
                        {
                            case "PING":
                                SendMessage(new("PONG", parameters: message.Parameters)); break;
                            case "USERSTATE":
                                m_Channel = message.GetCommand().Channel;
                                CanalManager.Emit(StreamGlassCanals.CHAT_JOINED, m_Channel);
                                break;
                            case "JOIN":
                                if (m_SelfUserInfo?.Login != message.Nick)
                                    CanalManager.Emit(StreamGlassCanals.USER_JOINED, message.Nick);
                                break;
                            case "USERLIST":
                                string[] users = message.Parameters.Split(' ');
                                foreach (string user in users)
                                {
                                    if (m_SelfUserInfo?.Login != user)
                                        CanalManager.Emit(StreamGlassCanals.USER_JOINED, user);
                                }
                                break;
                            case "PRIVMSG":
                                CreateUserMessage(message, false, false);
                                break;
                            case "USERNOTICE":
                                TreatUserNotice(message);
                                break;
                            case "LOGGED":
                                CanalManager.Emit(StreamGlassCanals.CHAT_CONNECTED);
                                if (!string.IsNullOrEmpty(m_Channel))
                                    SendMessage(new("JOIN", parameters: m_Channel));
                                break;
                            case "GLOBALUSERSTATE":
                                m_ChatColor = message.GetTag("color");
                                break;
                        }
                    }
                }
            } while (position >= 0);
        }

        public void Join(string channel) => SendMessage(new("JOIN", channel: string.Format("#{0}", channel)));

        public void SendMessage(string channel, string message)
        {
            Message messageToSend = new("PRIVMSG", channel: channel, parameters: message);
            SendMessage(messageToSend);
            CreateUserMessage(messageToSend, false, true);
        }

        private void SendMessage(Message message)
        {
            try
            {
                string messageData = message.ToString();
                Logger.Log("Twitch IRC", string.Format("=> {0}", Regex.Replace(messageData.Trim(), "(oauth:).+", "oauth:*****")));
                m_Client.Send(messageData);
            }
            catch (Exception e)
            {
                Logger.Log("Twitch IRC", string.Format("On send exception: {0}", e));
            }
        }

        protected override void OnClientDisconnect(int clientID)
        {
            Logger.Log("Twitch IRC", "Disconnecting");
        }

        protected override void OnWebSocketMessage(int clientID, string message)
        {
            TreatReceivedBuffer(message);
        }

        protected override void OnWebSocketOpen(int clientID, Quicksand.Web.Http.Response response)
        {
            SendAuth();
        }

        protected override void OnWebSocketClose(int clientID, short code, string closeMessage)
        {
            Logger.Log("Twitch IRC", string.Format("<=[Error] WebSocket closed ({0}): {1}", code, closeMessage));
        }

        protected override void OnWebSocketError(int clientID, string error)
        {
            Logger.Log("Twitch IRC", string.Format("<=[Error] WebSocket error: {0}", error));
        }

        protected override void OnWebSocketFrame(int clientID, Frame frame)
        {
            Logger.Log("Twitch IRC", string.Format("<=[WS] {0}", frame.ToString().Trim()));
        }

        protected override void OnWebSocketFrameSent(int clientID, Frame frame)
        {
            Logger.Log("Twitch IRC", string.Format("[WS]=> {0}", frame.ToString().Trim()));
        }

        internal void Disconnect()
        {
            m_Client.Disconnect();
        }
    }
}

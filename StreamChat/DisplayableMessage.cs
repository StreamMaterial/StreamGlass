﻿using System;
using System.Collections.Generic;

namespace StreamGlass.StreamChat
{
    public class DisplayableMessage
    {
        private readonly string m_RawMessage;
        private readonly string m_EmotelessMessage;
        private readonly List<Tuple<int, string>> m_Emotes = new();

        public string Message => m_RawMessage;
        public string EmotelessMessage => m_EmotelessMessage;
        public List<Tuple<int, string>> Emotes => m_Emotes;

        public DisplayableMessage(string message)
        {
            m_RawMessage = message;
            m_EmotelessMessage = message;
        }

        public DisplayableMessage(string rawMessage, string emotelessMessage)
        {
            m_RawMessage = rawMessage;
            m_EmotelessMessage = emotelessMessage;
        }

        public void AddEmote(int emotePos, string emoteID) => m_Emotes.Add(new(emotePos, emoteID));
    }
}

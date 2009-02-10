#region Disclaimer / License
// Copyright (C) 2009, Kenneth Skovhede
// http://www.hexad.dk, opensource@hexad.dk
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
// 
#endregion
using System;
using System.Collections.Generic;
using System.Text;

namespace Duplicati.Library.Main
{
    public class BackupEntry
    {
        public enum EntryType
        {
            Signature,
            Content,
            Manifest
        }

        private Duplicati.Library.Backend.FileEntry m_fileentry;
        private DateTime m_time;
        private List<BackupEntry> m_incrementals;
        private EntryType m_type;
        private List<BackupEntry> m_signature;
        private bool m_isFull;
        private bool m_isShortName;
        private string m_encryptionMode;
        private string m_compressionMode;
        private List<BackupEntry> m_content;

        public string Filename { get { return m_fileentry.Name; } }
        public Backend.FileEntry FileEntry { get { return m_fileentry; } }
        public DateTime Time { get { return m_time; } }
        public bool IsFull { get { return m_isFull; } }
        public bool IsShortName { get { return m_isShortName; } }
        public string EncryptionMode { get { return m_encryptionMode; } }
        public string CompressionMode { get { return m_compressionMode; } }
        public List<BackupEntry> Incrementals { get { return m_incrementals; } set { m_incrementals = value; } }
        public List<BackupEntry> ContentVolumes { get { return m_content; } }
        public List<BackupEntry> SignatureFile { get { return m_signature; } }
        public EntryType Type { get { return m_type; } }

        public BackupEntry(Backend.FileEntry fe, DateTime time, EntryType type, bool isFull, bool isShortName, string compressionMode, string encryptionMode)
        {
            m_fileentry = fe;
            m_time = time;
            m_type = type;
            m_isFull = isFull;
            m_isShortName = isShortName;
            m_compressionMode = compressionMode;
            m_encryptionMode = encryptionMode;
            m_signature = new List<BackupEntry>();
            m_content = new List<BackupEntry>();
            m_incrementals = new List<BackupEntry>();
        }
    }

    internal class Sorter : IComparer<BackupEntry>
    {
        #region IComparer<BackupEntry> Members

        public int Compare(BackupEntry x, BackupEntry y)
        {
            return x.Time.CompareTo(y.Time);
        }

        #endregion
    }

}

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace GeneralUpdate.Differential.ContentProvider
{
    public interface IFileProvider<TFile,TFileTree,TFileNode>
    {
        TFileTree Read(string path);

        IEnumerable<TFileNode> Compare(string leftPath,string rightPath);

        bool Equals(TFile leftFile,TFile rightFile);

        bool DeleteAll(string path);
    }
}

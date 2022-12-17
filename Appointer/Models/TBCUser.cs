using Auxiliary;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Appointer.Models
{
    public class TBCUser : BsonModel
    {
        private string _accountName = string.Empty;
        public string AccountName { get => _accountName; set { _ = this.SaveAsync(x => x.AccountName, value); _accountName = value; } }

        private int _playtime;
        public int Playtime { get => _playtime; set { _ = this.SaveAsync(x => x.Playtime, value); _playtime = value; } }
    }
}

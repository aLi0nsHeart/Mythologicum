namespace Mythologicum
{
    /// <summary>Stores message identities so a language change also updates an ongoing activity.</summary>
    internal sealed class StatusMessage
    {
        private string id;
        private object[] arguments;
        public StatusMessage(string id = "") => this.id = id;
        public string Value
        {
            get
            {
                if (arguments == null || arguments.Length == 0) return Loc.Resolve(id);
                var localizedArguments = new object[arguments.Length];
                for (int i = 0; i < arguments.Length; i++)
                    localizedArguments[i] = arguments[i] is string text ? Loc.Resolve(text) : arguments[i];
                return Loc.Format(id, localizedArguments);
            }
        }
        public void Set(string messageId, params object[] values) { id = messageId; arguments = values; }
    }
}

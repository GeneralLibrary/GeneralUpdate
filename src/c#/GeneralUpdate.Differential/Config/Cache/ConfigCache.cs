using System.Collections.Immutable;

namespace GeneralUpdate.Differential.Config.Cache
{
    public class ConfigCache<TEntity> : ICache<TEntity> where TEntity : class
    {
        #region Private Members

        private ImmutableDictionary<string, TEntity> _cache = null;
        private ImmutableDictionary<string, TEntity>.Builder _cacheBuilder = null;

        #endregion Private Members

        #region Constructors

        public ConfigCache()
        {
            _cacheBuilder = ImmutableDictionary.Create<string, TEntity>().ToBuilder();
        }

        #endregion Constructors

        #region Public Properties

        public ImmutableDictionary<string, TEntity> Cache { get => _cache; private set => _cache = value; }

        #endregion Public Properties

        #region Public Methods

        public void TryAdd(string key, TEntity entity)
        {
            if (!_cacheBuilder.ContainsKey(key)) _cacheBuilder.Add(key, entity);
        }

        public TEntity TryGet(string key)
        {
            TEntity result = null;
            if (_cacheBuilder.ContainsKey(key)) _cacheBuilder.TryGetValue(key, out result);
            return result;
        }

        public bool TryRemove(string key)
        {
            bool isRemove = false;
            if (_cacheBuilder.ContainsKey(key)) isRemove = _cacheBuilder.Remove(key);
            return isRemove;
        }

        public void Build()
        {
            if (Cache == null) Cache = _cacheBuilder.ToImmutableDictionary();
        }

        public void Dispose()
        {
            if (Cache != null)
            {
                Cache.Clear();
                Cache = null;
            }

            if (_cacheBuilder != null)
            {
                _cacheBuilder.Clear();
                _cacheBuilder = null;
            }
        }

        #endregion Public Methods
    }
}
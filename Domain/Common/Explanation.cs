using System.Collections.Concurrent;

namespace Domain.Common
{
    public class Explanation
    {
        private readonly ConcurrentDictionary<Identities, List<string>> _explanation = new ConcurrentDictionary<Identities, List<string>>();
        public Explanation()
        {
            _explanation.TryAdd(Identities.Nicodemus, new List<string> { 
                "He knows who is Scribes and Pharisees.",
                "He cannot cannot be exiled.",
                "He has the right to object to exiled once.",
                "He loss the right to object when he lost all his vote weight."
            });
            _explanation.TryAdd(Identities.Peter, new List<string> {
                "The important leader of Christians.",
                "If John is in the game, he will have the right to not be exiled (privilege) for once.",
                "His vote weight will increase 1 after Day 2."
            });
            _explanation.TryAdd(Identities.John, new List<string> {
                "The important leader of Christians.",
                "If present, Peter is granted the privilege once.",
                "Have the ability to descend heavenly fire, the weight of the descended person is reduced by half."
            });
            _explanation.TryAdd(Identities.Laity, new List<string> {
                "Ordinary believers, do not have any abilities.",
                "Find the Judaisms and remove their vote weights."
            });
            _explanation.TryAdd(Identities.Judas, new List<string> {
                "He can verify one person after the second night, and he will only get a confirmation when he checks a Christian.",
                "He will lost his ability when his lost all his vote weight.",
                "On the night of Day 3, he will meet the Priest and providing information."
            });
            _explanation.TryAdd(Identities.Preist, new List<string> {
                "He has the ability to try to exiled one person every night.",
                "He can meet with Nicodemus and Pharisees on the first night, but he will not know the exact identity.",
                "If Judas is in game, he can meet with Judas at the 3rd night."
            });
            _explanation.TryAdd(Identities.Pharisee, new List<string>
            {
                "He has the ability to know the party of the person who exiled last night.",
                "He can meet with the Preist and Nicodemus on the first night, but he will not know the exact identity."
            });
            _explanation.TryAdd(Identities.Judaism, new List<string>
            {
                "Ordinary believers, do not have any abilities.",
                "Find the Christians and remove their vote weights."
            });
        }

        public List<string>? getExplanation(Identities identity)
        {
            List<string>? abilityExplanation;
            _explanation.TryGetValue(identity, out abilityExplanation);
            return abilityExplanation;
        }
    }
}

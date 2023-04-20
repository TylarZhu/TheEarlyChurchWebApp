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
                "He cannot serve as priests and cannot be exiled.",
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
                "He can verify one person per night, but only if he verify a Christian, will get a confirmation.",
                "He will lost his ability when his lost all his vote weight.",
                "On the night of Day 2, he will meet the Priest and providing information."
            });
            _explanation.TryAdd(Identities.Scribes, new List<string> {
                "He have the chance to become the Priest",
                "If he did not becomes the Prist, he will be the Ruler of The Synagogue.",
                "If he is the Priest, he has the ability to try to exiled one person every night.",
                "If he is the Ruler of The Synagogue, he has the ability to know the party of the person who exiled last night.",
                "If he is the Ruler of The Synagogue, he will lost his ability when his lost all his vote weight."
            });
            _explanation.TryAdd(Identities.Pharisee, new List<string>
            {
                "He have the chance to become the Priest",
                "If he did not becomes the Prist, he will be the Ruler of The Synagogue.",
                "If he is the Priest, he has the ability to try to exiled one person every night.",
                "If he is the Ruler of The Synagogue, he has the ability to know the party of the person who exiled last night.",
                "If he is the Ruler of The Synagogue, he will lost his ability when his lost all his vote weight."
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

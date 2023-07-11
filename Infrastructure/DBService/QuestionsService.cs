using Domain.DBEntities;
using Domain.Interfaces;
using Infrastructure.MongoDBSetUp;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Domain.Common;
using System.Collections.Generic;

namespace Infrastructure.DBService
{
    public class QuestionsService: IQuestionsService
    {
        private readonly IMongoCollection<Questions> questionsService;
        private readonly IMongoDatabase mongoDb;

        public QuestionsService(IOptions<QuestionsSettings> QuestionSetting)
        {
            var mongoClient = new MongoClient(QuestionSetting.Value.ConnectionString);
            mongoDb = mongoClient.GetDatabase(QuestionSetting.Value.DatabaseName);
            questionsService = mongoDb.GetCollection<Questions>(QuestionSetting.Value.QuestionCollectionName);
        }

        public async Task InitQuestions()
        {
            if(!mongoDb.ListCollectionNames().ToList().Contains("QuestionCollection"))
            {
                List<Questions> questions = new List<Questions>() {
                new Questions(new Dictionary<string, string>() {
                    { "Q", "The New Testament consists of how many books, chapters, and verses?" },
                    { "A", "27 Books, 260 Chapters, 7,959 Verses" },
                    { "B", "39 Books, 929 Chapters, 23,214 Verses" },
                    { "C", "39 Books, 260 Chapters, 7,959 Verses" },
                    { "D", "27 Books, 929 Chapters, 23,214 Verses" },
                    { "An", "A" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "The New Testament is divided into which categories?" },
                    { "A", "2 Categories: Gospels, Historical Books" },
                    { "B", "3 Categories: Gospels, Historical Books, Letters" },
                    { "C", "4 Categories: Gospels, Historical Books, Letters, Revelation" },
                    { "D", "5 Categories: Gospels, Historical Books, Letters, Revelation, Pastoral Epistles" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "What does the name “Jesus” mean? (Matthew 1:21)" },
                    { "A", "God with us" },
                    { "B", "The Lord will prepare." },
                    { "C", "God’s grace" },
                    { "D", "God’s salvation, to deliver His people from sin." },
                    { "An", "D" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In Jesus’ genealogy, how many '14 generations' sets are there?" },
                    { "A", "3" },
                    { "B", "4" },
                    { "C", "5" },
                    { "D", "6" },
                    { "An", "A" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "What is the Lord’s first new command? (John 13: 34-35)" },
                    { "A", "Do testimonies" },
                    { "B", "Make all people disciples of Christ" },
                    { "C", "Love one another" },
                    { "D", "Live spiritually holy lives" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In 2 John 3:23, what is God’s command to us?" },
                    { "A", "Go and make disciples of all nations, baptizing them in the name of the Father and of the Son and of the Holy Spirit" },
                    { "B", "To believe in the name of His Son, Jesus Christ, and to love one another" },
                    { "C", "Love your neighbour as yourself" },
                    { "D", "The good shepherd lays down his life for the sheep" },
                    { "An", "B" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In 1 Thessalonians 4 and 5, what are the two wills of God?" },
                    { "A", "Lead a quiet life, mind your own business, and work with your hands" },
                    { "B", "To not be sanctified; avoid sexual immorality" },
                    { "C", "Rejoice always, pray continuously, give thanks in all circumstances" },
                    { "D", "Do not quench the Spirit" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In 2 Peter 1:5-7, what are the 8 steps of Spiritual growth for the path to Heaven?" },
                    { "A", "Faith, Goodness, Knowledge, Self-control, Perseverance, Godliness, Mutual Affection, Holy Communion" },
                    { "B", "Faith, Goodness, Knowledge, Self-control, Perseverance, Godliness, Mutual Affection, Prayer" },
                    { "C", "Faith, Goodness, Knowledge, Self-control, Perseverance, Godliness, Mutual Affection, Love for God" },
                    { "D", "Faith, Goodness, Knowledge, Self-control, Perseverance, Godliness, Mutual Affection, Love for One Another" },
                    { "An", "D" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "What are the 9 'fruits of the Spirit'?" },
                    { "A", "Love, Joy, Peace, Forbearance, Kindness, Goodness, Faithfulness, Gentleness, and Self-Control" },
                    { "B", "Love, Joy, Peace, Forbearance, Riches, Goodness, Faithfulness, Gentleness, and Self-Control" },
                    { "C", "Love, Joy, Peace, Forbearance, Kindness, Social Status, Faithfulness, Gentleness, and Self-Control" },
                    { "D", "Love, Joy, Peace, Forbearance, Kindness, Goodness, Faithfulness, Reputation, and Self-Control" },
                    { "An", "A" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In Galatians 5:19-21, Paul listed 15 acts of the flesh, which option below is not an act of the flesh?" },
                    { "A", "Sexual Immorality, Impurity, Debauchery, Idolatry" },
                    { "B", "Witchcraft, Hatred, Discord, Jealousy" },
                    { "C", "Fits of Rage, Selfish Ambition, Dissensions, Factions" },
                    { "D", "Envy, Drunkenness, Orgies, Laziness" },
                    { "An", "D" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In John 4:34, what is Jesus’ Spiritual food?" },
                    { "A", "Prayer & Sermons" },
                    { "B", "Do the Will & Finish Work" },
                    { "C", "Heal & Worship" },
                    { "D", "Praise & Interpret Scriptures" },
                    { "An", "D" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "According to John 6:35, 51, what is the Spiritual food for Christians?" },
                    { "A", "Repentance" },
                    { "B", "Praise" },
                    { "C", "Meditation" },
                    { "D", "Jesus" },
                    { "An", "D" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In Matthew 4:4, man shall not live by bread alone, what else does man need?" },
                    { "A", "Determination" },
                    { "B", "Thought" },
                    { "C", "Word of God" },
                    { "D", "Water" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In John 1:18, what/who revealed God to us?" },
                    { "A", "The Pope" },
                    { "B", "The One and Only Son of God" },
                    { "C", "Legends" },
                    { "D", "Science" },
                    { "An", "B" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In John 1:17, from whom was the law given through? From whom was grace and truth given?" },
                    { "A", "David, Moses" },
                    { "B", "Scribes, Pharisees" },
                    { "C", "Moses, Jesus" },
                    { "D", "The Nation, Knowledge" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "What is the relationship between Bartholomew and Nathaniel?" },
                    { "A", "The same person" },
                    { "B", "Cousins" },
                    { "C", "Brothers" },
                    { "D", "Friends" },
                    { "An", "A" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "What is the cross’ function? (1 Peter 2:24)" },
                    { "A", "Fashion" },
                    { "B", "Worship" },
                    { "C", "Salvation" },
                    { "D", "Tools for Torture" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In the four Gospels, how many times did God speak from Heaven?" },
                    { "A", "3" },
                    { "B", "4" },
                    { "C", "5" },
                    { "D", "6" },
                    { "An", "A" },
                    { "Ex", "Matthew 3:17, Matthew 17:5, John 12:28" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In the New Testament, which books referenced the Old Testament the least and most respectively?" },
                    { "A", "Matthew, Revelations" },
                    { "B", "Luke, Revelations" },
                    { "C", "Hebrews, Revelations" },
                    { "D", "Mark, Revelations" },
                    { "An", "D" },
                    { "Ex", "Mark 15 times, Matthew 19 times, Luke 25 times, Hebrews 85 times, Revelations 245 times"}
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In the 4 Gospels, where did Jesus speak the parables of the secrets of the kingdom of Heaven?" },
                    { "A", "Matthew 13" },
                    { "B", "Mark 13" },
                    { "C", "Luke 13" },
                    { "D", "John 13" },
                    { "An", "A" },
                    { "Ex", "Matthew 13: Parable of (1) The Sower, (2) Weeds, (3) Mustard Seed, (4) Yeast, (5) Hidden Treasure, (6) Pearl, (7) The Net" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "On which mountain did Jesus’ transfiguration occur?" },
                    { "A", "Mount Sinai" },
                    { "B", "Mount Olivet (Mount of Olives)" },
                    { "C", "Mount Hermon" },
                    { "D", "Mount Moriah" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "The Jordan river is in present-day where?" },
                    { "A", "Eastern Egypt" },
                    { "B", "Central Palestine" },
                    { "C", "Central Mesopotamia" },
                    { "D", "Sharjah regions" },
                    { "An", "B" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "According to 1 Timothy 1:5, the source of command is what?" },
                    { "A", "Law" },
                    { "B", "Good deeds" },
                    { "C", "Love" },
                    { "D", "Obedience" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "According to 1 Timothy 1:5, what does love originate from?" },
                    { "A", "A pure heart" },
                    { "B", "A good conscience" },
                    { "C", "A sincere faith" },
                    { "D", "All of the above" },
                    { "An", "D" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "According to 1 Corinthians 2:14-15, who can accept the things that come from the Spirit?" },
                    { "A", "Pastor" },
                    { "B", "The Pope" },
                    { "C", "A Person with Spirit" },
                    { "D", "A Person of Prayer" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "According to Matthew 12:50, who are Jesus’ brothers and sisters?" },
                    { "A", "Those who does the will of God" },
                    { "B", "James" },
                    { "C", "Mary" },
                    { "D", "Simon" },
                    { "An", "A" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Which of the books below are Paul’s letters of pastoral epistles?" },
                    { "A", "Titus" },
                    { "B", "Matthew" },
                    { "C", "Romans" },
                    { "D", "1 Peter" },
                    { "An", "A" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Before Jesus left His apostles, what did he bless them with?" },
                    { "A", "Leadership" },
                    { "B", "Ability to drive out demons" },
                    { "C", "Healing powers" },
                    { "D", "The Holy Spirit" },
                    { "An", "D" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "On the cross, who did Jesus leave his mother Mary in the care of?" },
                    { "A", "Peter" },
                    { "B", "John" },
                    { "C", "James" },
                    { "D", "Andrew" },
                    { "An", "B" },
                    { "Ex", "John 19: 26-27" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "The book of Romans was dictated by Paul, written by who?" },
                    { "A", "Luke" },
                    { "B", "Mark" },
                    { "C", "Tertius" },
                    { "D", "Titus" },
                    { "An", "C" },
                    { "Ex", "Romans 16: 22"}
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In the book of Romans, who is the sister Paul commended to the church?" },
                    { "A", "Phoebe" },
                    { "B", "Mary" },
                    { "C", "Lydia" },
                    { "D", "Martha" },
                    { "An", "A" },
                    { "Ex", "Romans 16: 1-2"}
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Of the 4 Gospels, which book proclaims Jesus to be the saviour of man?" },
                    { "A", "Matthew" },
                    { "B", "Mark" },
                    { "C", "Luke" },
                    { "D", "John" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "According to John 1:35-40, which of Jesus' 12 disciples were previously disciples to John the Baptist?" },
                    { "A", "Peter and James" },
                    { "B", "Andrew and John" },
                    { "C", "Peter and Judas" },
                    { "D", "Philip and Nathaniel" },
                    { "An", "B" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "What was the first miracle Jesus performed?" },
                    { "A", "Feed 5,000 men" },
                    { "B", "Calm the storm and sea" },
                    { "C", "Heal a blind man" },
                    { "D", "Turn water into wine" },
                    { "An", "D" },
                    { "Ex", "John 2: 1-11" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "John in his old age often said one thing, what was it?" },
                    { "A", "Love one another" },
                    { "B", "Do not sin" },
                    { "C", "Do not love the world or anything in it" },
                    { "D", "Keep yourselves from idols" },
                    { "An", "A" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "The book of Romans was written by Paul and Tertius in who’s household?" },
                    { "A", "Rufus" },
                    { "B", "Priscilla and Aquila" },
                    { "C", "Lydia" },
                    { "D", "Gaius" },
                    { "An", "D" },
                    { "Ex", "Romans 16: 23" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "On the cross Jesus said 'Eli, Eli, lema sabachthani' What does this mean?" },
                    { "A", "Father, Father please help me" },
                    { "B", "My God, my God, why have you forsaken me" },
                    { "C", "Eli, Eli, come follow me" },
                    { "D", "Lord, Lord, I will glorify your name" },
                    { "An", "B" },
                    { "Ex", "Matthew 27: 46" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Which book in the New Testament is referred to as 'the key to the Old Testament'?" },
                    { "A", "Philippians" },
                    { "B", "Colossians" },
                    { "C", "Hebrews" },
                    { "D", "Titus" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "What is the name of the young man wearing nothing but linen garment in Mark 14:51-52?" },
                    { "A", "Barnabas" },
                    { "B", "Luke" },
                    { "C", "Mark" },
                    { "D", "Paul" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Which of the 4 Gospel books was first written?" },
                    { "A", "Matthew" },
                    { "B", "Mark" },
                    { "C", "Luke" },
                    { "D", "John" },
                    { "An", "B" },
                    { "Ex", "Around 50 A.D/C.E" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Whom did Paul write the book of 1 Corinthians with?" },
                    { "A", "Gaius" },
                    { "B", "Sosthenes" },
                    { "C", "Tertius" },
                    { "D", "Apollos" },
                    { "An", "B" },
                    { "Ex", "1 Corinthians 1:1" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Who was Luke?" },
                    { "A", "A doctor" },
                    { "B", "A priest" },
                    { "C", "A scribe" },
                    { "D", "A merchant" },
                    { "An", "A" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Which books were written by Luke?" },
                    { "A", "Romans" },
                    { "B", "Acts" },
                    { "C", "Matthew" },
                    { "D", "1 Corinthians" },
                    { "An", "B" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Of the many authors who wrote the New Testament books, one was not Jewish. Who was he?" },
                    { "A", "Mark" },
                    { "B", "Juda" },
                    { "C", "Luke" },
                    { "D", "Paul" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "The Gospel of Luke was written by Luke to Theophilus. What does Theophilus mean?" },
                    { "A", "One who desires wisdom" },
                    { "B", "One who loves God" },
                    { "C", "One who desires truth" },
                    { "D", "One whom God blesses" },
                    { "An", "B" },
                    { "Ex", "Luke 1:1" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "During the time of the apostles 7 deacons were chosen, whom were full of the Spirit and wisdom. " +
                        "Which one of the pairs options below were not deacons?"
                    },
                    { "A", "Philip, Stephen" },
                    { "B", "Procorus, Nicanor" },
                    { "C", "Timon, Parmenas" },
                    { "D", "Nicolaus, Paul" },
                    { "An", "D" },
                    { "Ex", "Acts 6:5" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Who was the first person to be martyred for the Lord?" },
                    { "A", "Jacob" },
                    { "B", "Stephen" },
                    { "C", "Paul" },
                    { "D", "Peter" },
                    { "An", "B" },
                    { "Ex", "(Acts 7) Philip, Stephen"}
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "During Jesus’ transfiguration Peter offered to set up shelters for three people. Who does it not include?" },
                    { "A", "Jesus" },
                    { "B", "Moses" },
                    { "C", "Elijah" },
                    { "D", "Abraham" },
                    { "An", "D" },
                    { "Ex", "Matthew 17:4" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "John the Baptist offers what kind of baptism?" },
                    { "A", "Baptism of water" },
                    { "B", "Baptism of fire" },
                    { "C", "Baptism of repentance" },
                    { "D", "Baptism of the Holy Spirit" },
                    { "An", "C" },
                    { "Ex", "Acts 19:4" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Where in the Scriptures was the Lord’s Prayer recorded?" },
                    { "A", "Matthew" },
                    { "B", "Mark" },
                    { "C", "Peter" },
                    { "D", "John" },
                    { "An", "A" },
                    { "Ex", "Matthew 6:9-13, Luke 11:2-4"}
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "The Lord’s Prayer has three focuses, which one below is not included?" },
                    { "A", "Hallowed by your name" },
                    { "B", "Your kingdom come" },
                    { "C", "Your Spirit come" },
                    { "D", "Your will be done, on earth as it is in Heaven" },
                    { "An", "C" },
                    { "Ex", "Matthew 6:9-11"}
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Which chapter in the Gospel of Matthew does it record the Lord’s second coming?" },
                    { "A", "5" },
                    { "B", "6" },
                    { "C", "13" },
                    { "D", "24" },
                    { "An", "D" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "At what place were believers of Christ first referred to as 'Christians'?" },
                    { "A", "Jerusalem" },
                    { "B", "Antioch" },
                    { "C", "Samaria" },
                    { "D", "Rome" },
                    { "An", "B" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "What was the apostle Paul’s original name and what does it mean?" },
                    { "A", "Anna, meaning: blessing or prayer" },
                    { "B", "Jacob, meaning: to seize/grab" },
                    { "C", "Saul, meaning: to ask/pray for" },
                    { "D", "David, meaning: beloved" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Where was Timothy born?" },
                    { "A", "Lystra" },
                    { "B", "Tarsus" },
                    { "C", "Ephesus" },
                    { "D", "Colossae" },
                    { "An", "A" },
                    { "Ex", "Acts 16: 1" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Who climbed on top of a sycamore-fig tree to see Jesus?" },
                    { "A", "Gaius" },
                    { "B", "Zacchaeus" },
                    { "C", "Cain" },
                    { "D", "Titus" },
                    { "An", "B" },
                    { "Ex", "Luke 19: 1-10" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Where was Paul born?" },
                    { "A", "Smyrna of Asia" },
                    { "B", "Tarsus of Cilicia" },
                    { "C", "Cappadocia" },
                    { "D", "Galatia" },
                    { "An", "B" },
                    { "Ex", "Acts 21: 39" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "How many times was the phrase “Truly, truly I say to you” recorded in the Gospel of John?" },
                    { "A", "15" },
                    { "B", "20" },
                    { "C", "25" },
                    { "D", "30" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In the Gospel of John, Jesus said 'I am' 7 times. In the phrases below, which one is not a correct 'I am' phrase?" },
                    { "A", "The bread of life, light of the world" },
                    { "B", "The good shepherd, gate for the sheep" },
                    { "C", "Resurrection and the life, the way and the truth and the life" },
                    { "D", "Vine, Messiah" },
                    { "An", "D" },
                    { "Ex", "The bread of life (John 6: 35, 48, 51)\n" +
                        "The light of the world (John 8: 12, 9: 5)\n" +
                        "The good shepherd (John 10: 11, 14)\n" +
                        "The gate for the sheep (John 10: 7, 9)\n" +
                        "The resurrection and the life (John 11: 25)\n" +
                        "The way, the truth, and the life (John 14: 6)\n" +
                        "The true vine (John 15: 1, 5)"
                    }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In Galatians 6, if a Christian is caught in a sin, what should be done?" },
                    { "A", "Expelled from the church" },
                    { "B", "Be kept watch" },
                    { "C", "Restore that person gently, but watch yourself or you also may be tempted" },
                    { "D", "Wait until the coming judgment" },
                    { "An", "C" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Who lied to the Holy Spirit and died in front of the apostles?" },
                    { "A", "Barnabas" },
                    { "B", "Simon Magus (Simon the Sorcerer)" },
                    { "C", "Cornelius the Centurion" },
                    { "D", "Ananias" },
                    { "An", "D" },
                    { "Ex", "Acts 5:1-6" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "After the death of Judas who did the apostles choose as a replacement?" },
                    { "A", "Matthias" },
                    { "B", "Philip" },
                    { "C", "Stephen" },
                    { "D", "Nicolaus" },
                    { "An", "A" },
                    { "Ex", "Acts 1:26" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "In the Gospels Jesus resurrected three persons, which of the following was not one of the persons He resurrected?" },
                    { "A", "A blind man outside the city of David" },
                    { "B", "The daughter of Jairus" },
                    { "C", "The son of a widow from the town of Nain" },
                    { "D", "Lazarus of Bethany" },
                    { "An", "A" },
                    { "Ex", "People Jesus resurrected include:\n" +
                        "The daughter of Jairus (Luke 8:49-55)\n" +
                        "The son of a widow from the town of Nain (Luke 7:11-15)\n" +
                        "Lazarus of Bethany (John 11)"
                    }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Which one below is not one of the three closest disciples of Jesus?" },
                    { "A", "Peter" },
                    { "B", "James" },
                    { "C", "Andrew" },
                    { "D", "John" },
                    { "An", "C" },
                    { "Ex", "Matthew 26: 37, Luke 8:51" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Which one below is not one of the three archangels mentioned in the Bible?" },
                    { "A", "Gabriel" },
                    { "B", "Daniel" },
                    { "C", "Michael" },
                    { "D", "Lucifer (The Morningstar)" },
                    { "An", "B" },
                    { "Ex", "The three archangels mentioned in the Bible are:\n" +
                        "Gabriel (Luke 1:19)\n" +
                        "Michael (Jude 1: 9)\n" +
                        "Lucifer, aka Satan (Ezekiel 28: 14-16, Isaiah 14: 12)"
                    }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Which of the following was not one of the gifts given to baby Jesus by the three wiseman?" },
                    { "A", "Gold" },
                    { "B", "Jewels" },
                    { "C", "Frankincense" },
                    { "D", "Myrrh" },
                    { "An", "B" },
                    { "Ex", "Matthew 2:11" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "How many times did Jesus pray in the garden of Gethsemane?" },
                    { "A", "1" },
                    { "B", "2" },
                    { "C", "3" },
                    { "D", "4" },
                    { "An", "C" },
                    { "Ex", "Matthew 26: 36-46" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "What does suffering produce according to the Bible?" },
                    { "A", "Character" },
                    { "B", "Hope" },
                    { "C", "Shame" },
                    { "D", "Perseverance" },
                    { "An", "D" },
                    { "Ex", "Romans 5: 3" }
                }),
                new Questions(new Dictionary<string, string>() {
                    { "Q", "Who is the mediator between God and mankind?" },
                    { "A", "The Pope" },
                    { "B", "Bishop" },
                    { "C", "Pastor" },
                    { "D", "Jesus Christ" },
                    { "An", "D" },
                    { "Ex", "1 Timothy 2: 5" }
                })
            };
                foreach (Questions question in questions)
                {
                    await questionsService.InsertOneAsync(question);
                }
            }
        }

        public async Task<Questions?> RandomSelectAQuestion()
        {
            /*List<Questions> array = await questionsService.AsQueryable().Sample(1).ToListAsync();*/
            List<Questions> array = await questionsService.Find(_ => true).ToListAsync();
            Random rand = new Random();
            return array[rand.Next(array.Count)];
        }
    }
}

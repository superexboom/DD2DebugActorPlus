using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DD2DebugActorPlus
{
    internal static class PinyinSearch
    {
        private const int MaxIndexCacheSize = 8192;
        private static readonly Dictionary<string, SearchIndex> IndexCache = new Dictionary<string, SearchIndex>(StringComparer.Ordinal);
        private static Dictionary<char, string> _pinyinMap;

        private const string PinyinDataBase64 =
            "5LiAOnlpfOS4gTpkaW5nfOS4gzpxaXzkuIc6d2FufOS4iDp6aGFuZ3zkuIk6c2FufOS4ijpzaGFuZ3zkuIs6eGlhfOS4jTpidXzkuI46eXV85LiQ" +
            "OmdhaXzkuJE6Y2hvdXzkuJM6emh1YW585LiUOnFpZXzkuJY6c2hpfOS4mDpxaXV85LiaOnllfOS4mzpjb25nfOS4nDpkb25nfOS4nTpzaXzkuKI6" +
            "ZGl1fOS4pDpsaWFuZ3zkuKU6eWFufOS4pzpzYW5nfOS4qjpnZXzkuK06emhvbmd85LiwOmZlbmd85LiyOmNodWFufOS4tDpsaW585Li6OndlaXzk" +
            "uLs6emh1fOS4vTpsaXzkuL46anV85LmDOm5haXzkuYU6aml1fOS5iDptZXzkuYk6eWl85LmLOnpoaXzkuYw6d3V85LmOOmh1fOS5jzpmYXzkuZA6" +
            "bGV85LmWOmd1YWl85LmYOmNoZW5nfOS5nTpqaXV85LmeOnFpfOS5nzp5ZXzkuaA6eGl85LmhOnhpYW5nfOS5pjpzaHV85LmwOm1haXzkubE6bHVh" +
            "bnzkubM6cnV85LqGOmxlfOS6iDp5dXzkuok6emhlbmd85LqLOnNoaXzkuow6ZXJ85LqOOnl1fOS6jzprdWl85LqROnl1bnzkupI6aHV85LqUOnd1" +
            "fOS6lTpqaW5nfOS6mjp5YXzkups6eGllfOS6nzpqaXzkuqE6d2FuZ3zkuqQ6amlhb3zkuqY6eWl85LqnOmNoYW585LqrOnhpYW5nfOS6rTp0aW5n" +
            "fOS6rjpsaWFuZ3zkurI6cWlufOS6tTp4aWV85Lq6OnJlbnzku4A6c2hlbnzku4E6cmVufOS7gzpkaW5nfOS7hTpqaW585LuGOnB1fOS7hzpjaG91" +
            "fOS7ijpqaW585LuLOmppZXzku406cmVuZ3zku446Y29uZ3zku5M6Y2FuZ3zku5Q6emFpfOS7ljp0YXzku5c6emhhbmd85LuYOmZ1fOS7ozpkYWl8" +
            "5LukOmxpbmd85LulOnlpfOS7qjp5aXzku6w6bWVufOS7sDp5YW5nfOS7tjpqaWFufOS7tzpqaWF85Lu7OnJlbnzku706ZmVufOS7vzpmYW5nfOS8" +
            "gTpxaXzkvIo6eWl85LyNOnd1fOS8jjpqaXzkvI86ZnV85LyQOmZhfOS8kTp4aXV85LyXOnpob25nfOS8mDp5b3V85LyZOmh1b3zkvJo6aHVpfOS8" +
            "njpzYW585LyfOndlaXzkvKA6Y2h1YW585LykOnNoYW5nfOS8pjpsdW585LyqOndlaXzkvK86Ym985LywOmd1fOS8tDpiYW585Ly2Omxpbmd85Ly4" +
            "OnNoZW585Ly6OmNpfOS8vDpzaGl85Ly9OmdhfOS9hjpkYW585L2NOndlaXzkvY46ZGl85L2POnpodXzkvZE6eW91fOS9kzp0aXzkvZU6aGV85L2Z" +
            "Onl1fOS9mzpmdXzkvZw6enVvfOS9nTpnb3V85L2gOm5pfOS9ozp5b25nfOS9qTpwZWl85L2sOmxhb3zkva86eWFuZ3zkvbM6amlhfOS9vzpzaGl8" +
            "5L6DOmthbnzkvog6Y2hpfOS+izpsaXzkvo06c2hpfOS+mzpnb25nfOS+nTp5aXzkvqA6eGlhfOS+ozpsdnzkvqY6emhlbnzkvqc6Y2V85L6uOnd1" +
            "fOS+tTpxaW585L6/OmJpYW585L+DOmN1fOS/ijpqdW585L+XOnN1fOS/nTpiYW985L+hOnhpbnzkv6k6bGlhfOS/rjp4aXV85L+vOmZ1fOS/sTpq" +
            "dXzlgI06YmVpfOWAkjpkYW985YCUOmp1ZXzlgJg6dGFuZ3zlgJk6aG91fOWAnzpqaWV85YCmOmp1YW585YC6OnpoYWl85YC8OnpoaXzlgL46cWlu" +
            "Z3zlgYc6amlhfOWBjzpwaWFufOWBmjp6dW985YGcOnRpbmd85YGlOmppYW585YG2Om91fOWBtzp0b3V85YG7OmxvdXzlgb46ZmVufOWBvzpjaGFu" +
            "Z3zlgo06YmFuZ3zlgqg6Y2h1fOWCrDpjdWl85YKyOmFvfOWCuzpzaGF85YOPOnhpYW5nfOWDmjpsaWFvfOWDpzpzZW5nfOWDtTpqaWFuZ3zlg7s6" +
            "cGl85YSGOmppbmd85YSSOnJ1fOWEvzplcnzlhYE6eXVufOWFgzp5dWFufOWFhDp4aW9uZ3zlhYU6Y2hvbmd85YWGOnpoYW985YWIOnhpYW585YWJ" +
            "Omd1YW5nfOWFizprZXzlhY06bWlhbnzlhZE6ZHVpfOWFlDp0dXzlhZw6ZG91fOWFpTpydXzlhag6cXVhbnzlhas6YmF85YWsOmdvbmd85YWtOmxp" +
            "dXzlhbA6bGFufOWFsTpnb25nfOWFszpndWFufOWFtDp4aW5nfOWFtTpiaW5nfOWFtjpxaXzlhbc6anV85YW4OmRpYW585YW5OnppfOWFuzp5YW5n" +
            "fOWFvDpqaWFufOWFvTpzaG91fOWGhTpuZWl85YaMOmNlfOWGjTp6YWl85YaSOm1hb3zlhpc6cm9uZ3zlhpk6eGllfOWGmzpqdW585YacOm5vbmd8" +
            "5YagOmd1YW585YaiOnpob25nfOWGpDp5dWFufOWGpTptaW5nfOWGrDpkb25nfOWGsDpiaW5nfOWGsjpjaG9uZ3zlhrM6anVlfOWGtTprdWFuZ3zl" +
            "hrY6eWV85Ya3Omxlbmd85Ya7OmRvbmd85Ya9OmxpZXzlh4A6amluZ3zlh4Q6cWl85YeGOnpodW585YeJOmxpYW5nfOWHizpkaWFvfOWHjDpsaW5n" +
            "fOWHjzpqaWFufOWHkTpjb3V85YebOmxpbnzlh506bmluZ3zlh6A6aml85YehOmZhbnzlh606cGluZ3zlh686a2FpfOWHtjp4aW9uZ3zlh7g6dHV8" +
            "5Ye5OmFvfOWHujpjaHV85Ye7OmppfOWHvTpoYW585Ye/Onphb3zliIA6ZGFvfOWIgzpyZW585YiGOmZlbnzliIc6cWllfOWIjTpjaHV85YiOOndl" +
            "bnzliJE6eGluZ3zliJI6aHVhfOWIlzpsaWV85YiZOnplfOWImjpnYW5nfOWImzpjaHVhbmd85YidOmNodXzliKA6c2hhbnzliKQ6cGFufOWIqTps" +
            "aXzliKs6YmllfOWIrjpndWF85YiwOmRhb3zliLY6emhpfOWItzpzaHVhfOWIujpjaXzliLs6a2V85Yi9Omd1aXzliYE6ZHVvfOWJgjpqaXzliYo6" +
            "eHVlfOWJjTpxaWFufOWJkTpqaWFufOWJlDp0aXzliZY6cG91fOWJnDp3YW585YmlOmJvfOWJpzpqdXzliak6c2hlbmd85YmqOmppYW585YmvOmZ1" +
            "fOWJsjpnZXzlib06cGlhb3zlib86amlhb3zliog6cGl85YqbOmxpfOWKnTpxdWFufOWKnjpiYW585YqfOmdvbmd85YqgOmppYXzliqE6d3V85Yqj" +
            "OmxpZXzliqg6ZG9uZ3zliqk6emh1fOWKqjpudXzliqs6amllfOWKsTpsaXzlirI6amlufOWKszpsYW985Yq/OnNoaXzli4M6Ym985YuHOnlvbmd8" +
            "5YuJOm1pYW585YuLOnh1bnzli5I6bGVpfOWLnzptdXzli6Q6cWlufOWLujpzaGFvfOWLvjpnb3V85Yu/Ond1fOWMgDp5dW585YyFOmJhb3zljIY6" +
            "Y29uZ3zljJU6Yml85YyWOmh1YXzljJc6YmVpfOWMmTpzaGl85YygOmppYW5nfOWMozp4aWF85YyqOmZlaXzljK46a3VpfOWMuTpwaXzljLo6cXV8" +
            "5Yy7OnlpfOWMvzpuaXzljYE6c2hpfOWNgzpxaWFufOWNhzpzaGVuZ3zljYg6d3V85Y2JOmh1aXzljYo6YmFufOWNjjpodWF85Y2POnhpZXzljZE6" +
            "YmVpfOWNkjp6dXzljZM6emh1b3zljZU6ZGFufOWNljptYWl85Y2XOm5hbnzljZo6Ym985Y2cOmJvfOWNoDp6aGFufOWNoTprYXzljac6d2985Y2r" +
            "OndlaXzljbA6eWlufOWNsTp3ZWl85Y2zOmppfOWNtDpxdWV85Y21Omx1YW585Y23Omp1YW585Y24OnhpZXzljoQ6ZXzljoU6dGluZ3zljoY6bGl8" +
            "5Y6JOmxpfOWOizp5YXzljow6eWFufOWOlTpjZXzljpg6bGl85Y6aOmhvdXzljp86eXVhbnzljqI6eGlhbmd85Y6oOmNodXzljqk6aml1fOWOrjpz" +
            "aXzljrs6cXV85Y+COmNhbnzlj4g6eW91fOWPiTpjaGF85Y+KOmppfOWPizp5b3V85Y+MOnNodWFuZ3zlj406ZmFufOWPkTpmYXzlj5Y6cXV85Y+X" +
            "OnNob3V85Y+YOmJpYW585Y+ZOnh1fOWPmzpwYW585Y+fOnNvdXzlj6A6ZGllfOWPozprb3V85Y+kOmd1fOWPpTpqdXzlj6Y6bGluZ3zlj6g6ZGFv" +
            "fOWPqjp6aGl85Y+rOmppYW985Y+sOnpoYW985Y+uOmRpbmd85Y+vOmtlfOWPsDp0YWl85Y+yOnNoaXzlj7M6eW91fOWPtjp5ZXzlj7c6aGFvfOWP" +
            "uDpzaXzlj7k6dGFufOWPvDpkaWFvfOWPvTpqaXzlkIE6eHV85ZCDOmNoaXzlkIQ6Z2V85ZCIOmhlfOWQiTpqaXzlkIo6ZGlhb3zlkIw6dG9uZ3zl" +
            "kI06bWluZ3zlkI46aG91fOWQkDp0dXzlkJE6eGlhbmd85ZCTOnhpYXzlkJc6bWF85ZCbOmp1bnzlkJ06bGlufOWQnjp0dW585ZCfOnlpbnzlkKA6" +
            "ZmVpfOWQpjpmb3V85ZCnOmJhfOWQqzpoYW585ZCsOnRpbmd85ZCvOnFpfOWQsTp6aGl85ZC1OmNoYW985ZC4OnhpfOWQuTpjaHVpfOWQuzp3ZW58" +
            "5ZC8OmhvdXzlkL46d3V85ZGAOnlhfOWRgzplfOWRhjpkYWl85ZGIOmNoZW5nfOWRijpnYW985ZGQOm5hfOWRkzp5aXzlkZU6b3V85ZGXOmJlaXzl" +
            "kZg6eXVhbnzlkZs6cWlhbmd85ZGcOnd1fOWRojpuZXzlkaY6eW91fOWRqDp6aG91fOWRsTpndXzlkbI6Y2l85ZGzOndlaXzlkbU6aGV85ZG3Omdh" +
            "fOWRuDpwZWl85ZG7OnNoZW585ZG8Omh1fOWRvTptaW5nfOWRvzpxdXzlkoA6anV85ZKGOnBhb3zlkos6emF85ZKMOmhlfOWSjjpqaXV85ZKSOnpo" +
            "b3V85ZKUOmthfOWSlTpndXzlkpY6a2F85ZKZOmxvbmd85ZKaOmRvbmd85ZKoOnppfOWSqzp6aGl85ZKsOnlhb3zlkq86Z2V85ZKxOnphbnzlkrM6" +
            "a2V85ZK4OnhpYW585ZK9Onlhbnzlkr86eWl85ZOAOmFpfOWTgTpwaW585ZOGOmR1b3zlk4c6d2F85ZOIOmhhfOWTiTp6YWl85ZONOnhpYW5nfOWT" +
            "jjphaXzlk5A6a3Vhbmd85ZOROnlhfOWTkjpkYXzlk5c6aHVhfOWTnTpub25nfOWTnzp5b3zlk6U6Z2V85ZOmOm985ZOoOnNoYW985ZOqOm5hfOWT" +
            "rTprdXzlk646eGlhb3zlk7I6emhlfOWTvDpoZW5nfOWUhzpjaHVufOWUiTphaXzllJA6dGFuZ3zllJQ6d3V85ZSkOmh1YW585ZSsOmh1fOWUrjpz" +
            "aG91fOWUrzp3ZWl85ZSxOmNoYW5nfOWUvjp0dW985ZWDOmtlbnzllYY6c2hhbmd85ZWKOmF85ZWVOnRhb3zllZY6ZGFufOWVnDpjaHVhaXzllaU6" +
            "c2hhfOWVpjpsYXzllac6emV85ZWqOnBhfOWVrDpzZXzlla46bmllfOWVsDpsdW985ZW3Omxhbmd85ZW4OnhpYW985ZW8OnRpfOWWgjp3ZWl85ZaD" +
            "Om5hbnzlloQ6c2hhbnzllok6aG91fOWWijpoYW585ZaLOmRpZXzllo86bnVvfOWWlDpvfOWWmDpjaHVhbnzllpk6aHVpfOWWnDp4aXzllp06aGV8" +
            "5ZanOnh1YW585Za3OnBlbnzllr06bG91fOWXhTp4aXV85ZeTOnNhbmd85ZecOnNoaXzll506Z2V85ZehOndlbmd85ZejOnNpfOWXpjpzdW985Zes" +
            "OmhlfOWXrzpufOWXtzphb3zll706c291fOWYgDpkaXzlmIE6cWl85ZiIOmNhb3zlmIk6amlhfOWYjjpnYXzlmJg6eHV85ZibOm1hfOWYnzpkdXzl" +
            "mLI6Y2hhb3zlmLQ6enVpfOWYtjpzaXzlmLs6eGl85Zi/OmhlaXzlmY46eWV85ZmcOmx1fOWZojpvfOWZqDpxaXzlmak6ZXzlmas6eWl85ZmsOnNo" +
            "aXzlmbw6cGl85ZqOOmhhb3zlmo86dGl85ZqTOmNhfOWaozp4aWFvfOWarzpodW985Zq3OnJhbmd85Zq8Omp1ZXzlm4o6bmFuZ3zlm5o6cWl1fOWb" +
            "mzpzaXzlm546aHVpfOWboDp5aW585ZuiOnR1YW585ZukOmR1bnzlm606eXVhbnzlm7A6a3VufOWbtDp3ZWl85Zu5Omxpbmd85Zu6Omd1fOWbvTpn" +
            "dW985Zu+OnR1fOWchDp5dXzlnIY6eXVhbnzlnIg6cXVhbnzlnJw6aHVhbnzlnJ86dHV85ZyjOnNoZW5nfOWcqDp6YWl85ZywOmRpfOWcujpjaGFu" +
            "Z3zlnL46aml85Z2HOmp1bnzlnYo6ZmFuZ3zlnY46a2FufOWdjzpodWFpfOWdkDp6dW985Z2ROmtlbmd85Z2XOmt1YWl85Z2aOmppYW585Z2bOnRh" +
            "bnzlnZ06YmF85Z2fOmZlbnzlnaA6emh1aXzlnaE6cG985Z2mOnRhbnzlnbc6a2V85Z6COmNodWl85Z6DOmxhfOWeizp4aW5nfOWekjpsZWl85Z6g" +
            "OnlpbnzlnqM6eXVhbnzlnqs6ZGlhbnzlnq46a3VhfOWfgzphaXzln4s6bWFpfOWfjjpjaGVuZ3zln586eXV85Z+5OnBlaXzln7o6aml85aCCOnRh" +
            "bmd85aCGOmR1aXzloJE6cWlhbnzloJU6ZHVvfOWgoTpiYW985aCkOmRpfOWgqjprYW585aC1OmR1fOWhjDp0YXzloZE6c3V85aGUOnRhfOWhnjpz" +
            "YWl85aGrOnRpYW585aKDOmppbmd85aKTOm11fOWimTpxaWFuZ3zlop46emVuZ3zlop86eHV85aKoOm1vfOWjgTpiaXzlo5E6aGV85aOVOmhhb3zl" +
            "o6Q6cmFuZ3zlo6s6c2hpfOWjrDpyZW585aOuOnpodWFuZ3zlo7A6c2hlbmd85aOzOmtlfOWjtjpodXzlpIQ6Y2h1fOWkhzpiZWl85aSNOmZ1fOWk" +
            "jzp4aWF85aSVOnhpfOWkljp3YWl85aSaOmR1b3zlpJw6eWV85aSfOmdvdXzlpKc6ZGF85aSpOnRpYW585aSqOnRhaXzlpKs6ZnV85aStOnlhb3zl" +
            "pK46eWFuZ3zlpLE6c2hpfOWktDp0b3V85aS3OnlpfOWkuDprdWF85aS5OmppYXzlpLo6ZHVvfOWlhzpxaXzlpYg6bmFpfOWliTpmZW5nfOWlizpm" +
            "ZW585aWPOnpvdXzlpZE6cWl85aWUOmJlbnzlpZU6eWl85aWWOmppYW5nfOWllzp0YW985aWgOmRpYW585aWiOnNoZXzlpaU6YW985aWzOm52fOWl" +
            "tDpudXzlpbY6bmFpfOWluDpqaWFufOWluTp0YXzlpb06aGFvfOWmgjpydXzlpoQ6d2FuZ3zlpoY6emh1YW5nfOWmhzpmdXzlpog6bWF85aaSOmR1" +
            "fOWmljp5YW985aaZOm1pYW985aalOnR1b3zlpqg6ZmFuZ3zlprk6bWVpfOWmuzpxaXzlp4o6eml85aeLOnNoaXzlp5A6amllfOWnkTpndXzlp5M6" +
            "eGluZ3zlp5w6amlhbmd85ae/OnppfOWogTp3ZWl85aiDOndhfOWohzpqaWFvfOWomDpuaWFuZ3zlqLE6eXV85amGOnBvfOWpqjpsYW585am0Onlp" +
            "bmd85aqaOm1laXzlqrI6cGl85auBOmppYXzlq4k6aml85auMOnhpYW585aupOm5lbnzlrIk6eGl85a2QOnppfOWtlDprb25nfOWtlTp5dW585a2X" +
            "OnppfOWtmDpjdW585a2cOnppfOWtojpiYW985a2jOmppfOWtpDpndXzlraY6eHVlfOWtqTpoYWl85a2qOmx1YW585a2xOmNhbnzlrbU6ZnV85a29" +
            "Om5pZXzlroE6bmluZ3zlroM6dGF85a6FOnpoYWl85a6HOnl1fOWuiDpzaG91fOWuiTphbnzlrow6d2FufOWujzpob25nfOWulzp6b25nfOWumDpn" +
            "dWFufOWumTp6aG91fOWumjpkaW5nfOWumzp3YW585a6cOnlpfOWunTpiYW985a6eOnNoaXzlrqA6Y2hvbmd85a6hOnNoZW585a6iOmtlfOWuozp4" +
            "dWFufOWupDpzaGl85a6rOmdvbmd85a6wOnphaXzlrrM6aGFpfOWutDp5YW585a61OnhpYW985a62OmppYXzlrrk6cm9uZ3zlrr06a3Vhbnzlrr86" +
            "c3V85a+COmppfOWvhDpqaXzlr4Y6bWl85a+MOmZ1fOWvkDptZWl85a+SOmhhbnzlr506cWlufOWvnzpjaGF85a+hOmd1YXzlr6Q6d3V85a+lOmxp" +
            "YW985a+4OmN1bnzlr7k6ZHVpfOWvujpzaXzlr7s6eHVufOWvvDpkYW985a++OmR1aXzlsIE6ZmVuZ3zlsIQ6c2hlfOWwhjpqaWFuZ3zlsIo6enVu" +
            "fOWwjzp4aWFvfOWwkTpzaGFvfOWwlDplcnzlsJY6amlhbnzlsJg6Y2hlbnzlsJo6c2hhbmd85bCdOmNoYW5nfOWwpDp5b3V85bCxOmppdXzlsLg6" +
            "c2hpfOWwujpjaGl85bC8Om5pfOWwvTpqaW585bC+OndlaXzlsL86bmlhb3zlsYA6anV85bGBOnBpfOWxgjpjZW5nfOWxhTpqdXzlsYg6cXV85bGJ" +
            "OnRpfOWxijpqaWV85bGLOnd1fOWxjzpwaW5nfOWxkTp4aWV85bGVOnpoYW585bGeOnNodXzlsaA6dHV85bGhOmx2fOWxpTpsdnzlsbE6c2hhbnzl" +
            "sbk6eWl85bKBOnN1aXzlsoI6cWl85bKMOmppfOWylDpjaGF85bKWOnF1fOWylzpnYW5nfOWyqTp5YW585bKtOmxpbmd85bK4OmFufOWzoTp4aWF8" +
            "5bOwOmZlbmd85bO7Omp1bnzltIc6Y2hvbmd85bSOOnFpfOW0ljp5YXzltKk6YmVuZ3zltK06emhhbnzltL06emFpfOW1jDpxaWFufOW3hTpkaWFu" +
            "fOW3jTp3ZWl85bedOmNodWFufOW3njp6aG91fOW3oTp4dW585beiOmNoYW985belOmdvbmd85bemOnp1b3zlt6c6cWlhb3zlt6g6anV85bepOmdv" +
            "bmd85berOnd1fOW3rjpjaGF85bexOmppfOW3sjp5aXzlt7Q6YmF85be3OnhpYW5nfOW3vjpqaW585biBOmJpfOW4gjpzaGl85biDOmJ1fOW4hTpz" +
            "aHVhaXzluIY6ZmFufOW4iDpzaGl85biMOnhpfOW4kDp6aGFuZ3zluJU6cGF85biYOmxpYW585bicOnpoaXzluJ06ZGl85bimOmRhaXzluK06eGl8" +
            "5biuOmJhbmd85bi3OndlaXzluLg6Y2hhbmd85bi9Om1hb3zluYU6ZnV85bmVOm11fOW5sjpnYW585bmzOnBpbmd85bm0Om5pYW585bm2OmJpbmd8" +
            "5bm4Onhpbmd85bm7Omh1YW585bm8OnlvdXzlub06eW91fOW5vzpndWFuZ3zluoQ6emh1YW5nfOW6hjpxaW5nfOW6hzpiaXzluoo6Y2h1YW5nfOW6" +
            "jzp4dXzlupM6a3V85bqUOnlpbmd85bqVOmRpfOW6lzpkaWFufOW6mTptaWFvfOW6nDpmdXzlup46cGFuZ3zlup86ZmVpfOW6pjpkdXzluqc6enVv" +
            "fOW6rTp0aW5nfOW6tzprYW5nfOW6uDp5b25nfOW7iTpsaWFufOW7ijpsYW5nfOW7kzprdW985bu2Onlhbnzlu7c6dGluZ3zlu7o6amlhbnzlvIA6" +
            "a2FpfOW8gjp5aXzlvIM6cWl85byEOm5vbmd85byIOnlpfOW8ijpiaXzlvI86c2hpfOW8kzpnb25nfOW8lTp5aW585byXOmZ1fOW8mzpjaGl85byf" +
            "OmRpfOW8oDp6aGFuZ3zlvKU6bWl85bymOnhpYW585bypOm51fOW8rzp3YW585byxOnJ1b3zlvLk6ZGFufOW8ujpxaWFuZ3zlvZI6Z3VpfOW9kzpk" +
            "YW5nfOW9lTpsdXzlvZc6aHVpfOW9ojp4aW5nfOW9qTpjYWl85b2xOnlpbmd85b25OnlpfOW9uzpjaGV85b28OmJpfOW+gDp3YW5nfOW+gTp6aGVu" +
            "Z3zlvoQ6amluZ3zlvoU6ZGFpfOW+iDpoZW585b6KOmh1YWl85b6LOmx2fOW+kDp4dXzlvpI6dHV85b6XOmRlfOW+mDpwYWl85b6ZOnhpfOW+oTp5" +
            "dXzlvqo6eHVufOW+rjp3ZWl85b63OmRlfOW+vTpodWl85b+DOnhpbnzlv4U6Yml85b+GOnlpfOW/jDpqaXzlv406cmVufOW/jzpjaGFufOW/lzp6" +
            "aGl85b+YOndhbmd85b+ZOm1hbmd85b+cOnlpbmd85b+gOnpob25nfOW/pzp5b3V85b+rOmt1YWl85b+xOmNoZW585b+1Om5pYW585b+9Omh1fOaA" +
            "gDpodWFpfOaAgTp0YWl85oCCOnNvbmd85oCGOmNodWFuZ3zmgI46emVufOaAjzp5YW5nfOaAkjpudXzmgJU6cGF85oCWOmJ1fOaAnDpsaWFufOaA" +
            "nTpzaXzmgKA6ZGFpfOaAoTp5aXzmgKU6aml85oCnOnhpbmd85oCoOnl1YW585oCqOmd1YWl85oCvOnFpZXzmgLU6Y2h1fOaAuzp6b25nfOaBizps" +
            "aWFufOaBjTpodWFuZ3zmgZA6a29uZ3zmgZI6aGVuZ3zmgZU6c2h1fOaBmTp5YW5nfOaBojpodWl85oGoOmhlbnzmgak6ZW585oGqOmtlfOaBrTpn" +
            "b25nfOaBrzp4aXzmgbA6cWlhfOaBszprZW585oG2OmV85oG4OnRvbmd85oG7OmNlfOaBvDpuYW985oKEOnFpYW985oKJOnhpfOaCjTpoYW585oKU" +
            "Omh1aXzmgpY6YmVpfOaCmjpzb25nfOaCnzp3dXzmgqA6eW91fOaCozpodWFufOaCpjp5dWV85oKoOm5pbnzmgqw6eHVhbnzmgq86bWlufOaCsjpi" +
            "ZWl85oK0OmN1aXzmgrw6ZGFvfOaDhTpxaW5nfOaDijpqaW5nfOaDizp3YW585oOROmh1b3zmg5U6dGl85oOYOndhbmd85oOcOnhpfOaDnzp3ZWl8" +
            "5oOgOmh1aXzmg6Y6ZGlhbnzmg6c6anV85oOoOmNhbnzmg6k6Y2hlbmd85oOrOmJlaXzmg6w6cWllfOaDrTpjYW585oOuOmRhbnzmg686Z3Vhbnzm" +
            "g7A6ZHVvfOaDszp4aWFuZ3zmg7Y6aHVhbmd85oO5OnJlfOaEgTpjaG91fOaEhjpxaWFufOaEiDp5dXzmhIk6eXV85oSPOnlpfOaEmjp5dXzmhJ86" +
            "Z2FufOaEpDpmZW585oSnOmt1aXzmhKs6c3V85oS/Onl1YW585oWIOmNpfOaFizp0YWl85oWMOmh1YW5nfOaFjjpzaGVufOaFkTpzaGV85oWVOm11" +
            "fOaFojptYW585oWnOmh1aXzmhag6a2FpfOaFsDp3ZWl85oW3Omthbmd85oaLOmJpZXzmho46emVuZ3zmhpQ6cWlhb3zmhqc6Y2hvbmd85oaoOmhh" +
            "bnzmhqk6cWl85oasOmppbmd85oa+Omhhbnzmh4I6ZG9uZ3zmh4g6eGllfOaHijphb3zmh5E6bWVufOaHkjpsYW585oemOm51b3zmiIg6Z2V85oiP" +
            "OnhpfOaIkDpjaGVuZ3zmiJE6d2985oiSOmppZXzmiJU6cWlhbmd85oiWOmh1b3zmiJg6emhhbnzmiJs6amlhfOaInzpqaXzmiKY6emhhbnzmiKo6" +
            "amllfOaIrjpsdXzmiLM6Y2h1b3zmiLQ6ZGFpfOaItzpodXzmiL46bGl85oi/OmZhbmd85omAOnN1b3zmiYE6YmlhbnzmiYc6c2hhbnzmiYk6ZmVp" +
            "fOaJizpzaG91fOaJjTpjYWl85omOOnpoYXzmiZE6cHV85omSOmJhfOaJkzpkYXzmiZQ6cmVuZ3zmiZg6dHVvfOaJmzprYW5nfOaJozprb3V85omn" +
            "OnpoaXzmiak6a3VvfOaJqzpzYW985omsOnlhbmd85omtOm5pdXzmia46YmFufOaJrzpjaGV85omwOnJhb3zmibY6ZnV85om5OnBpfOaJvDplfOaJ" +
            "vjp6aGFvfOaJvzpjaGVuZ3zmioA6aml85oqEOmNoYW985oqJOmp1ZXzmioo6YmF85oqROnlpfOaKkjpzaHV85oqTOnpodWF85oqUOnBvdXzmipU6" +
            "dG91fOaKljpkb3V85oqXOmthbmd85oqYOnpoZXzmipo6ZnV85oqbOnBhb3zmiqA6a291fOaKoTpsdW585oqiOnFpYW5nfOaKpDpodXzmiqU6YmFv" +
            "fOaKqDpwZW5nfOaKqzpwaXzmiqw6dGFpfOaKsTpiYW985oq1OmRpfOaKuTptb3zmirw6eWF85oq9OmNob3V85ouCOmZ1fOaLhTpkYW585ouGOmNo" +
            "YWl85ouJOmxhfOaLjDpiYW585ouNOnBhaXzmi5A6Z3VhaXzmi5I6anV85ouTOnR1b3zmi5Q6YmF85ouWOnR1b3zmi5g6anV85ouZOnpodW985oub" +
            "OnpoYW985oucOmJhaXzmi586bml85ouiOmxvbmd85oujOmppYW585oulOnlvbmd85oumOmxhbnzmi6c6bmluZ3zmi6g6Ym985oupOnplfOaLrDpr" +
            "dW985outOnNoaXzmi686emhlbmd85ouxOmdvbmd85ouzOnF1YW585ou0OnNodWFufOaLtzprYW985ou8OnBpbnzmi706emh1YWl85ou+OnNoaXzm" +
            "i786bmF85oyBOmNoaXzmjII6Z3VhfOaMhzp6aGl85oyJOmFufOaMkTp0aWFvfOaMljp3YXzmjJo6emhpfOaMnjp0YXzmjJ86eGllfOaMoDpuYW98" +
            "5oyhOmRhbmd85oyjOnpoZW5nfOaMpDpqaXzmjKU6aHVpfOaMqDphaXzmjKo6bnVvfOaMqzpjdW985oyvOnpoZW585oy6OnRpbmd85oy9Ondhbnzm" +
            "jYI6d3V85o2FOnRvbmd85o2GOmt1bnzmjYk6emh1b3zmjYs6bHZ85o2POm5pZXzmjZA6anVhbnzmjZU6YnV85o2eOmxhb3zmjZ86c3VufOaNoTpq" +
            "aWFufOaNojpodWFufOaNozpkYW985o2nOnBlbmd85o2uOmp1fOaNtjpjaHVpfOaNtzpqaWV85o6AOnhpYW585o6IOnNob3V85o6JOmRpYW985o6M" +
            "OnpoYW5nfOaOjzp0YW985o6QOnFpYXzmjpI6cGFpfOaOljp5ZXzmjpg6anVlfOaOoDpsdmV85o6iOnRhbnzmjqU6amllfOaOpzprb25nfOaOqDp0" +
            "dWl85o6pOnlhbnzmjqo6Y3VvfOaOrDpqdXzmjrA6YmFpfOaOszpsdXzmjrc6emhpfOaOuDpkYW585o66OmNhbnzmj4k6cm91fOaPjTp6b3V85o+P" +
            "Om1pYW985o+QOnRpfOaPkjpjaGF85o+hOndvfOaPozpjaHVhaXzmj606amllfOaPtDp5dWFufOaQgTpnZXzmkIU6amlhb3zmkI86Ym985pCQOmNo" +
            "dXzmkJw6c291fOaQnjpnYW985pCsOmJhbnzmkK06ZGF85pC6OnhpZXzmkL06Y2hhfOaRgTplbnzmkYQ6c2hlfOaRhjpiYWl85pGHOnlhb3zmkYo6" +
            "dGFufOaRkjpiaW5nfOaRlDpzaHVhaXzmkZg6emhhaXzmkZ46bHVvfOaRpzpjdWl85pGpOm1vfOaRuDptb3zmkpE6Y2hlbmd85pKSOnNhfOaSlTpz" +
            "aXzmkp46emh1YW5nfOaSpDpjaGV85pKpOmxpYW985pKsOnFpYW985pKtOmJvfOaSrjpjdW985pK4Omx1fOaSvDpoYW585pOCOmxlaXzmk4U6c2hh" +
            "bnzmk406Y2FvfOaTkjpxaW585pOkOnhpbmd85pOmOmNhfOaUgDpwYW585pSSOnphbnzmlKU6enVhbnzmlKs6anVlfOaUrzp6aGl85pS2OnNob3V8" +
            "5pS4OnlvdXzmlLk6Z2FpfOaUuzpnb25nfOaUvjpmYW5nfOaUvzp6aGVuZ3zmlYU6Z3V85pWIOnhpYW985pWMOmRpfOaVjzptaW585pWROmppdXzm" +
            "lZk6amlhb3zmlZs6bGlhbnzmlZ06Yml85pWeOmNoYW5nfOaVojpnYW585pWjOnNhbnzmlaY6ZHVufOaVrDpqaW5nfOaVsDpzaHV85pWyOnFpYW98" +
            "5pW0OnpoZW5nfOaVtzpmdXzmloc6d2VufOaWkDpmZWl85paROmJhbnzmlpc6ZG91fOaWmTpsaWFvfOaWnDp4aWV85pafOnpoZW585pakOmppbnzm" +
            "lqU6Y2hpfOaWpzpmdXzmlqk6emhhbnzmlq06ZHVhbnzmlq86c2l85pawOnhpbnzmlrk6ZmFuZ3zmlr06c2hpfOaXgTpwYW5nfOaXhTpsdnzml4s6" +
            "eHVhbnzml486enV85peXOnFpfOaXoDp3dXzml6I6aml85pelOnJpfOaXpjpkYW585penOmppdXzml6g6emhpfOaXqTp6YW985pexOmhhbnzml7Y6" +
            "c2hpfOaXtzprdWFuZ3zml7o6d2FuZ3zmmII6YW5nfOaYjjptaW5nfOaYjzpodW585piTOnlpfOaYlDp4aXzmmJ86eGluZ3zmmKA6eWluZ3zmmKU6" +
            "Y2h1bnzmmKc6bWVpfOaYqDp6dW985pitOnpoYW985pivOnNoaXzmmL46eGlhbnzmmYM6aHVhbmd85pmLOmppbnzmmZI6c2hhaXzmmZM6eGlhb3zm" +
            "mZU6eXVufOaZljpodWl85pmaOndhbnzmmaY6aHVpfOaZqDpjaGVufOaZrjpwdXzmma86amluZ3zmmbA6eGl85pm0OnFpbmd85pm2Omppbmd85pm3" +
            "Omd1aXzmmbo6emhpfOaZvjpsaWFuZ3zmmoI6emFufOaahzp4aWF85pqWOm51YW585pqXOmFufOaarjptdXzmmrQ6YmFvfOabnDp5YW985puyOnF1" +
            "fOabszp5ZXzmm7Q6Z2VuZ3zmm7w6bWFufOabvjpjZW5nfOabvzp0aXzmnIA6enVpfOaciDp5dWV85pyJOnlvdXzmnIs6cGVuZ3zmnI06ZnV85pyX" +
            "Omxhbmd85pybOndhbmd85pydOmNoYW985pyfOnFpfOacpjptZW5nfOacqDptdXzmnKo6d2VpfOacqzptb3zmnKw6YmVufOacrzpzaHV85py0OnB1" +
            "fOactTpkdW985py6OmppfOacvTp4aXV85p2AOnNoYXzmnYI6emF85p2DOnF1YW585p2GOmdhbnzmnY46bGl85p2QOmNhaXzmnZE6Y3VufOadnzpz" +
            "aHV85p2gOmdhbmd85p2hOnRpYW985p2lOmxhaXzmnag6eWFuZ3zmna86YmVpfOadsDpqaWV85p2zOnlhb3zmnbU6Y2h1fOadvjpzb25nfOadvzpi" +
            "YW585p6BOmppfOaehDpnb3V85p6QOnhpfOaelTp6aGVufOaelzpsaW585p6aOm1laXzmnpw6Z3VvfOaenTp6aGl85p6iOnNodXzmnqo6cWlhbmd8" +
            "5p6tOnhpYW985p6vOmt1fOaetjpqaWF85p63OmppYXzmn4Q6YmluZ3zmn5A6bW91fOafkzpyYW585p+UOnJvdXzmn5w6Z3VpfOafpTpjaGF85p+s" +
            "OmppYW585p+xOnpodXzmn7M6bGl1fOaftDpjaGFpfOaghzpiaWFvfOagiDp6aGFufOagjzpsYW585qCROnNodXzmoJM6c2h1YW585qCWOnFpfOag" +
            "lzpsaXzmoKE6eGlhb3zmoKo6emh1fOagtzp5YW5nfOaguDpoZXzmoLk6Z2VufOagvDpnZXzmoL06emFpfOahhjprdWFuZ3zmoYg6YW585qGMOnpo" +
            "dW985qGOOnpoaXzmoZE6c2FuZ3zmoaM6ZGFuZ3zmoaU6cWlhb3zmoak6emh1YW5nfOahtjp0b25nfOaigTpsaWFuZ3zmooU6bWVpfOaijzpndXzm" +
            "opc6Z2VuZ3zmoqY6bWVuZ3zmoq06c3VvfOairzp0aXzmorA6eGllfOaiszpzaHV85qOAOmppYW585qOCOmxpbmd85qOLOnFpfOajjTpndW585qOS" +
            "OmJhbmd85qOaOnBlbmd85qOuOnNlbnzmo7E6bGVuZ3zmo7U6a2V85qO6Omd1YW585qSBOmd1b3zmpIU6eWl85qSNOnpoaXzmpI46Y2h1aXzmpJI6" +
            "amlhb3zmpZo6Y2h1fOalrTp5ZXzmpbw6bG91fOalvTpsZXzmpoI6Z2FpfOamhjp5dXzmppw6YmFuZ3zmprQ6bGl1fOamuzp0YXzmp4E6Z2FvfOan" +
            "jDpjaHVpfOanmzprYW585qe9OmNhb3zmqKE6bW985qiqOmhlbmd85qi1OnFpYW985qmhOnhpYW5nfOapsTpjaHV85qqQOnlhbnzmrKA6cWlhbnzm" +
            "rKE6Y2l85qyiOmh1YW585qyjOnhpbnzmrLI6eXV85qy6OnFpfOasvjprdWFufOathzp4aWV85q2JOnFpYW585q2MOmdlfOatojp6aGl85q2jOnpo" +
            "ZW5nfOatpDpjaXzmraU6YnV85q2mOnd1fOatpzpxaXzmrao6d2FpfOatuTpkYWl85q27OnNpfOatvDpqaWFufOaugzp5YW5nfOauhDp0aWFufOau" +
            "hjpkYWl85q6JOnh1bnzmroo6c2h1fOauizpjYW585q6SOnl1bnzmrrQ6b3V85q61OmR1YW585q63Onlpbnzmrr86ZGlhbnzmr4E6aHVpfOavgjpn" +
            "dXzmr4U6eWl85q+LOnd1fOavjTptdXzmr486bWVpfOavkjpkdXzmr5Q6Yml85q+VOmJpfOavmTpiaXzmr5s6bWFvfOavqzpoYW985q+vOnRhbnzm" +
            "sI86c2hpfOawkTptaW585rCUOnFpfOawmzpmZW585rCqOmtlfOawtDpzaHVpfOawuDp5b25nfOaxgTp6aGl85rGCOnFpdXzmsYc6aHVpfOaxiTpo" +
            "YW585rGQOnhpfOaxlzpoYW585rGfOmppYW5nfOaxoDpjaGl85rGhOnd1fOaxpDp0YW5nfOaxqTpndXzmsbI6aml85rG5Onhpb25nfOaxvTpxaXzm" +
            "soM6d2985rKJOmNoZW585rKMOmR1bnzmspA6bXV85rKTOmRhfOaymTpzaGF85rKbOnBlaXzmsp86Z291fOayoTptZWl85rKlOmxpfOaypjpsdW58" +
            "5rKnOmNhbmd85rKrOm1vfOayrjpqdXzmsrE6dHVvfOayszpoZXzmsrg6ZmVpfOayuTp5b3V85rK7OnpoaXzmsrw6emhhb3zmsr46emhhbnzmsr86" +
            "eWFufOazhDp4aWV85rOJOnF1YW585rOKOnBvfOazjDptaXzms5U6ZmF85rObOmZhbnzms546bmluZ3zms6E6cGFvfOazojpib3zms6M6cWl85rOl" +
            "Om5pfOazqDp6aHV85rOqOmxlaXzms686bWlufOazsDp0YWl85rO7OnhpZXzms7w6cG985rO9OnplfOa0gTpqaWV85rSLOnlhbmd85rSSOnNhfOa0" +
            "lzp4aXzmtJs6bHVvfOa0njpkb25nfOa0pTpqaW585rSqOmhvbmd85rSyOnpob3V85rS7Omh1b3zmtLw6d2F85rS+OnBhaXzmtYE6bGl1fOa1gzpq" +
            "aWF85rWFOnFpYW585rWGOmppYW5nfOa1hzpqaWFvfOa1ijp6aHVvfOa1izpjZXzmtY46aml85rWPOmxpdXzmtZE6aHVufOa1kzpub25nfOa1qTpo" +
            "YW985rWqOmxhbmd85rWuOmZ1fOa1tDp5dXzmtbc6aGFpfOa1uDpqaW585raCOnR1fOa2hTpuaWV85raIOnhpYW985raJOnNoZXzmtow6eW9uZ3zm" +
            "to46eGlhbnzmtpU6dGl85rabOnRhb3zmtp86bGlhbnzmtqE6d2985rajOmh1YW585rakOmRpfOa2pjpydW585raoOnpoYW5nfOa2qTpzZXzmtq86" +
            "eWF85rayOnllfOa2uDpoZXzmt4A6ZGlhbnzmt4U6eGl85reLOmxpbnzmt4w6dGFuZ3zmt5E6c2h1fOa3oTpkYW585rekOnl1fOa3rDpjdWl85rex" +
            "OnNoZW585rezOmNodW585re3Omh1bnzmt7k6eWFufOa3uzp0aWFufOa4hTpxaW5nfOa4ijp5dWFufOa4jTp6aXzmuI46ZHV85riQOmppYW585riU" +
            "Onl1fOa4lzpzaGVufOa4nTp5dXzmuKA6cXV85rihOmR1fOa4ozp6aGF85ripOndlbnzmuK86Z2FuZ3zmuLQ6a2V85ri4OnlvdXzmuLo6bWlhb3zm" +
            "uYM6cGFpfOa5jjptaWFufOa5ljpodXzmuZs6emhhbnzmua46eWFufOa5vzpzaGl85rqDOmt1aXzmuoU6amlhbnzmupA6eXVhbnzmupw6bGl1fOa6" +
            "ojp5aXzmuqo6eGl85rq2OnJvbmd85rq6Om5pfOa7gjpwYW5nfOa7izp6aXzmu5E6aHVhfOa7kzp6aXzmu5Q6dGFvfOa7mjpndW585rueOnpoaXzm" +
            "u6E6bWFufOa7pTpsYW585ruoOmJpbnzmu6k6dGFufOa7tDpkaXzmvII6cGlhb3zmvIY6cWl85ryJOmx1fOa8jzpsb3V85ryTOmxpfOa8lDp5YW58" +
            "5rygOm1vfOa8qTp4dWFufOa8qjp5aXzmvKs6bWFufOa9hzp4aWFvfOa9nDpxaWFufOa9pjpsYW985r2tOnRhbnzmva46Y2hhb3zmvoQ6Y2hlbmd8" +
            "5r6IOmNoZXzmvo46cGVuZ3zmvpw6bGFufOa+oTp6YW985r+AOmppfOa/kjpiaW5854CaOmhhbnzngYw6Z3Vhbnzngas6aHVvfOeBrTptaWV854Gv" +
            "OmRlbmd854GwOmh1aXzngbU6bGluZ3zngbY6emFvfOeBvDp6aHVvfOeBvjp6YWl854G/OmNhbnzngok6bHV854KKOmNodWl854KOOnlhbnzngpY6" +
            "ZHVufOeCmTp6aGl854KrOnh1YW5854KsOmp1fOeCrTp0YW5854KuOnBhb3zngrg6emhhfOeCuTpkaWFufOeCvDpsaWFufOeCvTpjaGl854OBOnNo" +
            "dW9854OCOmxhbnzng4g6bGllfOeDmTpsYW9854ObOnpodXzng586eWFufOeDpDprYW9854OmOmZhbnzng6c6c2hhb3zng6s6dGFuZ3zng6w6amlu" +
            "fOeDrTpyZXzng7k6cGVuZ3znhIk6eWFufOeEijpoYW5854SVOmh1YW5854SWOm1lbnznhJo6ZmVufOeEpjpqaWFvfOeEsDp5YW5854S2OnJhbnzn" +
            "hYw6aHVhbmd854WOOmppYW5854WeOnNoYXznhac6emhhb3znha46emh1fOeFvTpzaGFufOeGhDp4aXznhoo6eGlvbmd854aPOnh1bnznhpQ6cm9u" +
            "Z3znhp86c2h1fOeGoDp5aXznhqw6YW9854axOnJlfOeGtTpzaGFuZ3znh4M6cmFufOeHjjpsaWFvfOeHlDpmYW5854elOnphb3znh6c6c3VpfOeI" +
            "hjpiYW9854iqOnpoYW9854isOnBhfOeIsTphaXzniLU6anVlfOeItjpmdXzniLc6eWV854i9OnNodWFuZ3zniYc6cGlhbnzniYg6YmFufOeJjDpw" +
            "YWl854mZOnlhfOeJmzpuaXV854miOmxhb3zniac6bXV854mpOnd1fOeJsjpzaGVuZ3znibU6cWlhbnznibk6dGV854m6OnhpfOeKgDp4aXznioE6" +
            "bGl854qsOnF1YW5854qvOmZhbnznirY6emh1YW5nfOeKuTp5b3V854uCOmt1YW5nfOeLiDpiZWl854uQOmh1fOeLlzpnb3V854ueOm5pbmd854ug" +
            "Omhlbnzni6E6amlhb3zni6k6c2hvdXzni6w6ZHV854utOnhpYXzni646c2hpfOeLsDp6aGVuZ3zni7E6eXV854u8Omxhbmd854yOOmxpZXznjJY6" +
            "Y2hhbmd854ybOm1lbmd854ycOmNhaXznjJ06Y3V854yhOmx1b3znjKk6eGluZ3znjKo6emh1fOeMqzptYW9854yuOnhpYW5854y+Omh1YXznjZc6" +
            "anVlfOeNoDpsaWFvfOeOhzpsdnznjok6eXV8546LOndhbmd8546bOm1hfOeOqTp3YW58546rOm1laXznjq86aHVhbnznjrA6eGlhbnznjrc6ZGlh" +
            "bnznj4o6c2hhbnznj406emhlbnznj6A6emh1fOePrTpiYW5854++OnhpYW5855CDOnFpdXznkIU6bGFuZ3znkIY6bGl855CQOnN1b3znkKI6enVv" +
            "fOeQszpsaW5855C0OnFpbnznkLw6cWlvbmd855GVOnhpYXznkZk6bmFvfOeRmjpodXznkZ86c2V855GwOmd1aXznkoA6Y3VpfOeSgzpsaXznkqc6" +
            "Yml855KoOmNhbnznkrA6aHVhbnznk5w6Z3VhfOeTozpiYW5855OmOndhfOeTtjpwaW5nfOeUmDpnYW5855SaOnNoZW5855ScOnRpYW5855SfOnNo" +
            "ZW5nfOeUqDp5b25nfOeUqTpzaHVhaXznlLA6dGlhbnznlLE6eW91fOeUsjpqaWF855SzOnNoZW5855S1OmRpYW5855S3Om5hbnznlLg6ZGlhbnzn" +
            "lLs6aHVhfOeVhTpjaGFuZ3znlYw6amllfOeVjzp3ZWl855WUOnBhbnznlZk6bGl1fOeVnDpjaHV855WlOmx2ZXznlao6ZmFufOeVtDpjaG91fOeV" +
            "uDpqaXznlo86c2h1fOeWkTp5aXznlpc6bGlhb3znlpk6Z2V855akOmJhfOeWqzp5aXznlq46Y2h1YW5nfOeWrzpmZW5nfOeWsjpwaXznlrU6Y2l8" +
            "55a8OnRlbmd855a9Omp1fOeWvjpqaXznl4I6amlhfOeXhTpiaW5nfOeXhzp6aGVuZ3znl4o6cXVhbnznl406eWl855eSOnlhbmd855eUOnpoaXzn" +
            "l5U6aGVufOeXmzp0b25nfOeXojpsaXznl6g6bGFvfOeXqjpodWFufOeXsDp0YW5855e0OmNoaXznl7k6Yml855iZOnNhb3znmJ86d2VufOeYoDpq" +
            "aXznmKQ6bGl1fOeYpjpzaG91fOeYqTpkYXznmKo6YmllfOeYqzp0YW5855i0OnpoYW5nfOeYuDpxdWV855mMOmFpfOeZljpwaXznmas6ZGlhbnzn" +
            "mbo6ZmF855m7OmRlbmd855m9OmJhaXznmb46YmFpfOeagjp6YW9855qEOmRlfOeahjpqaWV855qHOmh1YW5nfOeaiDpndWl855qZOnhpfOearjpw" +
            "aXznmrE6emhvdXznm4Y6cGVufOebiDp5aW5nfOebijp5aXznm486emhhbnznm5A6eWFufOebkTpqaWFufOebkjpoZXznm5Q6a3VpfOebljpnYWl8" +
            "55uXOmRhb3znm5g6cGFufOebmzpzaGVuZ3znm586bWVuZ3znm646bXV855uvOmRpbmd855uyOm1hbmd855u0OnpoaXznm7g6eGlhbmd855u5OmR1" +
            "bnznm746ZHVufOecgTpzaGVuZ3znnIg6ZGFufOeciTptZWl855yLOmthbnznnJ86emhlbnznnKA6bWlhbnznnKY6eml855yoOnpoYXznnKk6eHVh" +
            "bnznnK86bWl855y2Omt1YW5nfOectzpqdWFufOecujp0aWFvfOecvDp5YW58552AOnpoZXznnYE6emhlbmd8552ROmppYW58552aOnlhfOedmzpq" +
            "aW5nfOedoTpzaHVpfOedozpkdXznnaU6cGl8552mOm11fOedqDpuaXznnas6amllfOeduTpkdXznnoQ6bWlhb3znnow6a2V8556OOnhpYXznnpE6" +
            "bWluZ3znnpI6bWFufOeeoDpjaGVuZ3znnqU6cGllfOeepzpxaWFvfOeeqjpkZW5nfOeerDpzaHVufOeerTpsaWFvfOeesDprYW58556zOnRvbmd8" +
            "55+bOm1hb3znn5w6amlufOefojpzaGl855+jOnlpfOefpTp6aGl855+rOmppYW9855+tOmR1YW5855+uOmFpfOefszpzaGl855+/Omt1YW5nfOeg" +
            "gTptYXznoII6c2hhfOegjDpxaXznoI06a2FufOeglDp5YW5856CWOnpodWFufOegpTpkaXznoLA6cGVuZ3znoLQ6cG9856C4OnphfOegujpsaXzn" +
            "oL46bGl856GAOmNodXznoZ06eGlhb3znoas6bGl1fOehrDp5aW5nfOehrjpxdWV856KMOmx1fOeijTphaXznoo46c3VpfOeikTpiZWl856KXOndh" +
            "bnznop86ZGllfOeisDpwZW5nfOeisTpqaWFufOeivjpuaWFufOejizpjdW9856OQOnBhbnzno6g6bW9856O6Omh1YW5nfOekujpzaGl856S8Omxp" +
            "fOekvjpzaGV856WAOnNpfOelhzpxaXznpYg6cWl856WWOnp1fOelmzpxdXznpZ06emh1fOelnjpzaGVufOelnzpzdWl856WlOnhpYW5nfOelqDpw" +
            "aWFvfOelrTpqaXznpbc6ZGFvfOeluDpodW9856aBOmppbnznpoU6Y2hhbnznpo86ZnV856a7OmxpfOemvTpxaW5856eAOnhpdXznp4E6c2l856eD" +
            "OnR1fOeniTpiaW5nfOenizpxaXV856eNOnpob25nfOenkTprZXznp5I6bWlhb3znp5g6bWl856ekOmNoZW5nfOenqTp6aGl856evOmppfOensDpj" +
            "aGVuZ3znp7s6eWl856e9Omh1aXznqIA6eGl856iLOmNoZW5nfOeojTpzaGFvfOeojjpzaHVpfOeomjp6aGl856igOmNob3V856izOndlbnznqLs6" +
            "ZGFvfOeovDpqaWF856i/Omdhb3znqYY6bXV856m0Onh1ZXznqbY6aml1fOeptzpxaW9uZ3znqbk6cWlvbmd856m6Omtvbmd856m/OmNodWFufOeq" +
            "gTp0dXznqoM6cWllfOeqhDp6aGFpfOeqjTpxaWFvfOeqkjp6aGl856qWOmppYW9856qXOmNodWFuZ3znqpg6amlvbmd856qcOmN1YW5856qdOndv" +
            "fOeqnzprdXznqqU6a3VpfOerizpsaXznq5Y6c2h1fOermTp6aGFufOernjpqaW5nfOernzpqaW5nfOeroDp6aGFuZ3znq6U6dG9uZ3znq606amll" +
            "fOerrzpkdWFufOervTp5dXznrIM6ZHV856yROnhpYW9856yUOmJpfOesmzpkaXznrJ46Y2hpfOespjpmdXznrKg6YmVufOesrDpkaXznrLw6bG9u" +
            "Z3znrYk6ZGVuZ3znrYs6amlufOetkTp6aHV8562SOnRvbmd8562UOmRhfOetljpjZXznrZs6c2hhaXznrbk6Y2hvdXznrb46cWlhbnznroA6amlh" +
            "bnznroc6Z2V8566XOnN1YW58566hOmd1YW58566tOmppYW58566xOnhpYW5nfOevhzpwaWFufOevkTprdWl856+dOmdvdXznr6E6Y3Vhbnznr7c6" +
            "cGVuZ3znsIc6Y3V857CnOmh1YW5nfOewqjp6YW5857C4OmJvfOexgTpsYWl857GNOmppfOexszptaXznsbs6bGVpfOeyiTpmZW5857KXOmN1fOey" +
            "mDp6aGFufOeypTp6aG91fOeyqjpmZW5857KuOmxpYW5nfOeysTpsaWFuZ3znsrk6Y3VpfOeyvjpqaW5nfOezijpodXzns5U6Z2FvfOezmTpjYW98" +
            "57OfOnphb3zns7s6eGl857SgOnN1fOe0ojpzdW9857SnOmppbnzntK86bGVpfOe1hDp6dXznta46eHV857mBOmZhbnznuqA6aml1fOe6ojpob25n" +
            "fOe6pDp4aWFufOe6pjp5dWV857qnOmppfOe6qjpqaXznuq86Y2h1bnznurI6Z2FuZ3znurM6bmF857q1Onpvbmd857q3OmZlbnznurg6emhpfOe6" +
            "uTp3ZW5857q6OmZhbmd857q9Om5pdXznur86eGlhbnznu4M6bGlhbnznu4Q6enV857uFOnNoZW5857uGOnhpfOe7hzp6aGl857uIOnpob25nfOe7" +
            "ijpiYW5857uMOmNodXznu406c2hhb3znu486amluZ3znu5E6YmFuZ3znu5I6cm9uZ3znu5M6amllfOe7lTpyYW9857uYOmh1aXznu5k6Z2VpfOe7" +
            "nDpsdW9857udOmp1ZXznu546amlhb3znu586dG9uZ3znu6M6eGl1fOe7pzpqaXznu6k6aml857uqOnh1fOe7rTp4dXznu7M6c2hlbmd857u0Ondl" +
            "aXznu7U6bWlhbnznu7Y6c2hvdXznu7c6YmVuZ3znu7g6Y2hvdXznu7w6em9uZ3znu706emhhbnznu786bHZ857yAOnpodWl857yFOm1pYW5857yO" +
            "OmR1YW5857yTOmh1YW5857yUOmRpfOe8lTpsdnznvJY6YmlhbnznvJg6eXVhbnznvJo6ZnV857ydOmZlbmd857ygOmNoYW5857ypOnN1b3znvKo6" +
            "bW91fOe8rjpzaGFufOe8sDpqaWFuZ3znvLQ6amlhb3znvLo6cXVlfOe9kDpndWFufOe9kTp3YW5nfOe9lDp3YW5nfOe9lTpoYW58572XOmx1b3zn" +
            "vZo6ZmF8572iOmJhfOe9qTp6aGFvfOe9qjp6dWl8572uOnpoaXznvbI6c2h1fOe+gTpqaXznvoo6eWFuZ3znvo46bWVpfOe+njp4aXV8576hOnhp" +
            "YW58576kOnF1bnznvrg6bGVpfOe+uTpnZW5nfOe+vTp5dXznv4U6Y2hpfOe/lDp4aWFuZ3znv6k6cGlhbnznv7E6YW9857+7OmZhbnznv7w6eWl8" +
            "6ICAOnlhb3zogIE6bGFvfOiAgzprYW986ICFOnpoZXzogIw6ZXJ86ICNOnNodWF86ICQOm5haXzogJU6Z2VuZ3zogJc6aGFvfOiAmDp5dW586ICz" +
            "OmVyfOiAuDpzb25nfOiAuzpjaGl86IC9OmRhbnzogL86Z2VuZ3zogYY6bGluZ3zogYo6bGlhb3zogYs6bG9uZ3zogYw6emhpfOiBlDpsaWFufOiB" +
            "mjpqdXzogao6Y29uZ3zogoM6c3V86IKGOnNpfOiCiTpyb3V86IKLOmxlfOiCjDpqaXzogpM6aHVhbmd86IKaOmR1fOiCnTpnYW586IKgOmNoYW5n" +
            "fOiCoTpndXzogqI6emhpfOiCpDpmdXzogqU6ZmVpfOiCqTpqaWFufOiCqjpmYW5nfOiCrjphbmd86IKvOmtlbnzogrI6eXV86IK0Onlhb3zogro6" +
            "ZmVpfOiCvjpzaGVufOiCvzp6aG9uZ3zog4A6emhhbmd86IOBOnhpZXzog4M6d2VpfOiDhjpkYW586IOMOmJlaXzog446dGFpfOiDljpwYW5nfOiD" +
            "nDpzaGVuZ3zog546YmFvfOiDoTpodXzog6c6bG9uZ3zog686a3VhfOiDszpnZXzog7Y6amlhb3zog7g6eGlvbmd86IO9Om5lbmd86ISCOnpoaXzo" +
            "hIY6Y3VpfOiEiTptYWl86ISKOmppfOiEjzp6YW5nfOiEkTpuYW986ISTOm5vbmd86ISWOmJvfOiEmjpqaWFvfOiEsTp0dW986IS4OmxpYW586IS+" +
            "OnBpfOiFijpsYXzohYw6eWFufOiFkDpmdXzohZQ6cWlhbmd86IWVOndhbnzohaU6eGluZ3zohbA6eWFvfOiFsTpqaWFufOiFuTpmdXzohbo6eGlh" +
            "bnzohbs6bml86IW+OnRlbmd86IW/OnR1aXzohoA6YmFuZ3zohoo6Ym986IaPOmdhb3zohpg6Ymlhb3zohps6dGFuZ3zohpw6bW986IadOnhpfOiG" +
            "qDpwZW5nfOiHgjpiaXzoh4M6eW9uZ3zoh4Y6eWl86IejOmNoZW586IeqOnppfOiHrTpjaG91fOiHszp6aGl86Ie0OnpoaXzoh7s6emhlbnzoh746" +
            "eXV86IiMOnNoZXzoiI06c2hlfOiIkDpzaGl86IiSOnNodXzoiJQ6dGlhbnzoiJs6Y2h1YW586IieOnd1fOiInzp6aG91fOiIqjpoYW5nfOiIrDpi" +
            "YW586IiwOmppYW586IixOmNhbmd86Ii5OmNodWFufOiJmDpzb3V86ImvOmxpYW5nfOiJsDpqaWFufOiJsjpzZXzoibM6eWFufOiJujp5aXzoib46" +
            "YWl86IqCOmppZXzoipI6bWFuZ3zoipw6d3V86IqdOnpoaXzoiqw6ZmVufOiKsTpodWF86IqzOmZhbmd86Iq4Onl1bnzoir06eWF86IuNOmNhbmd8" +
            "6IuPOnN1fOiLlDp0YWl86IuXOm1pYW986IufOmdvdXzoi6U6cnVvfOiLpjprdXzoi7E6eWluZ3zoi7k6cGluZ3zojIM6ZmFufOiMhDpqaWF86Iyn" +
            "OmppYW586IyrOm1hbmd86Iy2OmNoYXzojYY6amluZ3zojYk6Y2FvfOiNkDpqaWFufOiNkjpodWFuZ3zojZo6amlhfOiNnzpodWl86I2hOmRhbmd8" +
            "6I2jOnJvbmd86I2nOnlpbmd86I2vOnlhb3zojbc6aGV86I6rOm1vfOiOtzpodW986I66Onlpbmd86I69Om1hbmd86I+HOmd1fOiPjDpqdW586I+c" +
            "OmNhaXzoj7I6ZmVpfOiQgzpjdWl86JCEOnRhb3zokI46d2VpfOiQpDp5aW5nfOiQpTp5aW5nfOiQpjp5aW5nfOiQpzp4aWFvfOiQqDpzYXzokL06" +
            "bHVvfOiRlzp6aHV86JGbOmdlfOiRoTpwdXzokaM6ZG9uZ3zokaw6emFuZ3zokoI6ZGl86JKZOm1lbmd86JK4OnpoZW5nfOiSujpqaXzok4Q6eHV8" +
            "6JOdOmxhbnzolJE6bWllfOiUkzptYW586JSaOndlaXzolKw6c2h1fOiUvTpiaXzolYg6eHVufOiVqDpqdWV86JW0Onl1bnzolb46bGVpfOiWhDpi" +
            "YW986JaqOnhpbnzolrA6eHVufOiXiTpqaXzol486Y2FuZ3zol5A6bWlhb3zol5M6eGlhbnzol5w6bGl86JekOnRlbmd86Je7Onphb3zomJE6bW98" +
            "6Ji4OnpoYW586JmOOmh1fOiZkDpudmV86JmROmx2fOiZlDpxaWFufOiZmjp4dXzomZ46eXV86JmrOmNob25nfOiZrDpxaXV86JmxOnNoaXzomb06" +
            "c3VpfOiagDpzaGl86JqBOnlpfOiagjptYXzomoo6d2VufOialTpjYW586JqkOnphb3zom4A6emh1fOibhjpxdXzom4c6c2hlfOibijpndXzom4s6" +
            "ZGFufOibmTp3YXzom5s6emh1fOibrTp6aGl86JuuOm1hbnzom7A6emhlfOibvjplfOicgjpmZW5nfOicgzpzaGVufOicjTpjaHV86JyTOnRpbmd8" +
            "6JyVOnR1aXzonJc6d2986JyYOnpoaXzonJo6ZmVpfOicnDptaXzonKE6bGF86JyxOnBpfOicuzpxaW5nfOidhzp5aW5nfOidjjp4aWV86J2XOmh1" +
            "YW5nfOidmTpiaWFufOidoDpmdXzonbw6bG91fOiegjpsYW5nfOiejTpyb25nfOieqDptYW586J+ROnpoYW5nfOifvjpjaGFufOiglTpydXzooKI6" +
            "Y2h1bnzooYA6eHVlfOihhTp4aW586KGMOnhpbmd86KGNOnlhbnzooZQ6eGlhbnzooZc6amllfOihoTpoZW5nfOihozp5aXzooaU6YnV86KGoOmJp" +
            "YW986KGrOnNoYW586KGsOmNoZW586KGwOnNodWFpfOihtzp6aG9uZ3zoooU6bmlhb3zooos6ZGFpfOiijTpwYW986KKWOnhpdXzoopw6d2F86KKk" +
            "Om1hb3zooqs6YmVpfOiirTp4aXzoorE6ZnV86KOBOmNhaXzoo4I6bGllfOijhTp6aHVhbmd86KOUOnlpfOijlTp5dXzoo5k6cXVufOijpDprdXzo" +
            "o6g6Yml86KO4Omx1b3zoo7k6Z3VvfOikmzpsdnzopLQ6bGFufOilvzp4aXzopoE6eWFvfOimhjpmdXzop4E6amlhbnzop4I6Z3Vhbnzop4Q6Z3Vp" +
            "fOinhTptaXzop4Y6c2hpfOiniDpsYW586KeJOmp1ZXzop4o6aml86KeOOnl1fOinkTpxdXzop5I6amlhb3zop6M6amllfOinpjpjaHV86KiAOnlh" +
            "bnzoqbk6emhhbnzoqok6eXV86KqNOnJlbnzoqpM6c2hpfOiqnjp5dXzoqr86ZGlhb3zorZg6c2hpfOitpjpqaW5nfOiuoTpqaXzorqI6ZGluZ3zo" +
            "rqQ6cmVufOiupTpqaXzorqc6aG9uZ3zorqg6dGFvfOiuqTpyYW5nfOiurTp4dW586K6uOnlpfOiurzp4dW586K6wOmppfOiusjpqaWFuZ3zorrY6" +
            "eWF86K64Onh1fOiuujpsdW586K69OmZlbmd86K6+OnNoZXzorr86ZmFuZ3zor4E6emhlbmd86K+EOnBpbmd86K+FOnp1fOivhjpzaGl86K+IOnpo" +
            "YXzor4k6c3V86K+KOnpoZW586K+NOmNpfOivjzp6aGFvfOivkTp5aXzor5U6c2hpfOivlzpzaGl86K+aOmNoZW5nfOivmzp6aHV86K+dOmh1YXzo" +
            "r546ZGFufOivnzpnb3V86K+hOmd1aXzor6I6eHVufOivozp5aXzor6U6Z2FpfOivpjp4aWFuZ3zor6k6eHV86K+rOmppZXzor606eXV86K+vOnd1" +
            "fOivsTp5b3V86K+yOmh1aXzor7Q6c2h1b3zor7U6c29uZ3zor7c6cWluZ3zor7g6emh1fOivujpudW986K+7OmR1fOivvjprZXzor786d2VpfOiw" +
            "gTpzaHVpfOiwgzpkaWFvfOiwhTpsaWFuZ3zosIg6dGFufOiwijp5aXzosIs6bW91fOiwjjpodWFuZ3zosJA6eGllfOiwkzp3ZWl86LCVOnl1fOiw" +
            "mTphbnzosJs6ZGl86LCcOm1pfOiwojp4aWV86LCjOnlhb3zosKY6cWlhbnzosKc6bWl86LCoOmppbnzosKw6bWl1fOiwsTpwdXzosLI6anVlfOiw" +
            "tDpxaWFufOiwtzpndXzosYY6ZG91fOixoTp4aWFuZ3zosao6aGFvfOixqzp5dXzosbg6emhpfOixujpjaGFpfOiyjDptYW986LSdOmJlaXzotJ86" +
            "ZnV86LShOmdvbmd86LSiOmNhaXzotKM6emV86LSkOnhpYW586LSlOmJhaXzotKY6emhhbmd86LSnOmh1b3zotKg6emhpfOi0qTpmYW586LSqOnRh" +
            "bnzotKs6cGlufOi0rTpnb3V86LSuOnpodXzotK86Z3VhbnzotLE6amlhbnzotLQ6dGllfOi0tTpndWl86LS4Om1hb3zotLk6ZmVpfOi0vDp6ZWl8" +
            "6LWEOnppfOi1hTpnYWl86LWLOmZ1fOi1jDpkdXzotY46c2h1fOi1jzpzaGFuZ3zotZA6Y2l86LWWOmxhaXzotZg6emh1aXzotZ46emFufOi1oDp6" +
            "ZW5nfOi1ojp5aW5nfOi1pDpjaGl86LWmOnNoZXzotas6aGV86LWwOnpvdXzotbQ6ZnV86LW2Omdhbnzotbc6cWl86LaBOmNoZW586LaFOmNoYW98" +
            "6LaKOnl1ZXzotp86dGFuZ3zotqM6cXV86LazOnp1fOi2tDpwYXzotr46emhpfOi3gzp5dWV86LeEOnFpYW5nfOi3izpiYXzot4w6ZGllfOi3jjp0" +
            "dW986LeROnBhb3zot5o6c2hhbnzot5s6Ym986LedOmp1fOi3nzpnZW586LeoOmt1YXzot6o6Z3VpfOi3rzpsdXzot7M6dGlhb3zot7U6amlhbnzo" +
            "t7o6ZHVvfOi4iTpsaWFuZ3zouI86dGF86LidOmh1YWl86LieOmp1fOi4ojp0aXzouKk6Y2FpfOi4qjp6b25nfOi4sTpkdW986Li1Onpob25nfOi4" +
            "vTpqdXzouYQ6dGl86LmIOmRhb3zouYk6Y3VvfOi5ijpxaXzouYs6dGF86LmROm5pZXzouZI6cGFufOi5qTpiaWV86LmsOmRlbmd86LmtOmNlbmd8" +
            "6Lm2Omp1ZXzouoE6emFvfOi6qzpzaGVufOi6rDpnb25nfOi6rzpxdXzourI6ZHVvfOi6ujp0YW5nfOi9pjpjaGV86L2sOnpodWFufOi9rjpsdW58" +
            "6L2vOnJ1YW586L2wOmhvbmd86L2xOmd1fOi9tDp6aG91fOi9tjp5aXzovbs6cWluZ3zovb06emFpfOi+gzpqaWFvfOi+hTpmdXzovoY6bGlhbmd8" +
            "6L6IOmJlaXzovok6aHVpfOi+kTpqaXzovpM6c2h1fOi+lzpuaWFufOi+mDpsdXzovpk6emhlfOi+mzp4aW586L6cOmd1fOi+njpjaXzovp86cGl8" +
            "6L6jOmxhfOi+qDpiaWFufOi+qTpiaWFufOi+sDpjaGVufOi+sTpydXzovrk6Ymlhbnzovr06bGlhb3zovr46ZGF86L+BOnFpYW586L+EOnFpfOi/" +
            "hTp4dW586L+HOmd1b3zov4g6bWFpfOi/jjp5aW5nfOi/kDp5dW586L+ROmppbnzov5Q6ZmFufOi/mDpoYWl86L+ZOnpoZXzov5s6amlufOi/nDp5" +
            "dWFufOi/nTp3ZWl86L+eOmxpYW586L+fOmNoaXzov6U6amlvbmd86L+qOmRpfOi/qzpwb3zov606ZGllfOi/sDpzaHV86L+3Om1pfOi/uDpiZW5n" +
            "fOi/uTpqaXzov706emh1aXzpgIA6dHVpfOmAgTpzb25nfOmAgjpzaGl86YCDOnRhb3zpgIY6bml86YCJOnh1YW586YCKOnh1bnzpgI06eGlhb3zp" +
            "gI86dG91fOmAkDp6aHV86YCSOmRpfOmAlDp0dXzpgJc6ZG91fOmAmjp0b25nfOmAnTpzaGl86YCeOmNoZW5nfOmAnzpzdXzpgKA6emFvfOmAojpm" +
            "ZW5nfOmArjpkYWl86YC4OnlpfOmAuzpsdW986YC8OmJpfOmAvjp5dXzpgYE6ZHVufOmBgjpzdWl86YGHOnl1fOmBjTpiaWFufOmBjzplfOmBkzpk" +
            "YW986YGXOnlpfOmBmzpsaXV86YGiOnRhfOmBozpxaWFufOmBpTp5YW986YGoOmFvfOmBrTp6YW986YGuOnpoZXzpgbU6enVufOmBvzpiaXzpgoA6" +
            "eWFvfOmCgzpzdWl86YKLOmxhfOmCozpuYXzpgqY6YmFuZ3zpgqo6eGllfOmCrjp5b3V86YK4OmRpfOmCuzpsaW586YOBOnl1fOmDijpqaWFvfOmD" +
            "oTpqdW586YOoOmJ1fOmDvTpkb3V86YSZOmJpfOmFijpkaW5nfOmFjDp6aHVvfOmFjTpwZWl86YWSOmppdXzphZc6eHV86YWjOmhhbnzphaU6c3V8" +
            "6YWpOm1pbmd86YWqOmxhb3zphaw6Y2hvdXzphbE6amlhbmd86YW1OmppYW986YW3Omt1fOmFuDpzdWFufOmFvzpuaWFuZ3zphoc6Y2h1bnzphok6" +
            "enVpfOmGizpjdXzphpI6eGluZ3zph4c6Y2FpfOmHijpzaGl86YeMOmxpfOmHjTp6aG9uZ3zph446eWV86YePOmxpYW5nfOmHkTpqaW586YecOmZ1" +
            "fOmJtDpqaWFufOmPljphb3zpkog6emhlbnzpkok6ZGluZ3zpkp06ZHVufOmSnzp6aG9uZ3zpkqI6Z2FuZ3zpkqU6eWFvfOmSpjpxaW586ZKnOmp1" +
            "bnzpkqk6Z291fOmSrjpuaXV86ZKxOnFpYW586ZKzOnFpYW586ZK1OmJvfOmSuzp6dWFufOmTgTp0aWV86ZODOmxpbmd86ZOFOnFpYW586ZOQOmth" +
            "b3zpk5s6ZGFuZ3zpk5w6dG9uZ3zpk606bWluZ3zpk7I6Y2hhbnzpk7Y6eWlufOmTuDp6aHV86ZO6OnB1fOmTvjpsaWFufOmUgDp4aWFvfOmUgTpz" +
            "dW986ZSFOmd1b3zplIg6eGl1fOmUizpmZW5nfOmUjDp4aW586ZSPOmppYW586ZSQOnJ1aXzplJk6Y3VvfOmUmjptYW986ZSiOmd1fOmUpDpjaHVp" +
            "fOmUpTp6aHVpfOmUrjpqaWFufOmUrzpqdXzplLU6cWlhbmd86ZS7OmR1YW586ZWAOmR1fOmVgTptZWl86ZWHOnpoZW586ZWQOmdhb3zplZY6Ymlh" +
            "b3zplZw6amluZ3zplaM6bGlhb3zplbA6bGlhbnzplbY6eGlhbmd86ZW/OnpoYW5nfOmWizprYWl86ZaiOmd1YW586ZeoOm1lbnzpl6k6c2h1YW58" +
            "6ZeqOnNoYW586ZetOmJpfOmXrjp3ZW586ZevOmNodWFuZ3zpl7I6eGlhbnzpl7Q6amlhbnzpl7c6bWVufOmXuTpuYW986Ze7OndlbnzpmIE6Z2V8" +
            "6ZiFOnl1ZXzpmIg6eXV86ZiUOmt1b3zpmJ86ZHVpfOmYsTpqaW5nfOmYsjpmYW5nfOmYszp5YW5nfOmYtDp5aW586Zi1OnpoZW586Zi2OmppZXzp" +
            "mLs6enV86Zi/OmF86ZmEOmZ1fOmZhTpqaXzpmYY6bHV86ZmIOmNoZW586ZmLOmxvdXzpmYw6bW986ZmNOmppYW5nfOmZkDp4aWFufOmZojp5dWFu" +
            "fOmZpDpjaHV86ZmoOnl1bnzpmak6eGlhbnzpmao6cGVpfOmZtzp4aWFufOmahTp5dXzpmoY6bG9uZ3zpmo86c3VpfOmakDp5aW586ZqUOmdlfOma" +
            "mTp4aXzpmpw6emhhbmd86ZqnOnN1aXzpmrY6bGl86Zq8OnN1bnzpmr46bmFufOmbgDpxdWV86ZuBOnlhbnzpm4Q6eGlvbmd86ZuFOnlhfOmbhjpq" +
            "aXzpm4c6Z3V86ZuPOmNodXzpm5U6ZGlhb3zpm6g6eXV86ZuqOnh1ZXzpm7Y6bGluZ3zpm7c6bGVpfOmbvjp3dXzpnIA6eHV86ZyEOnhpYW986ZyG" +
            "OnRpbmd86ZyHOnpoZW586ZyJOm1laXzpnI06aHVvfOmcnDpzaHVhbmd86ZywOnhpYW586ZyyOmx1fOmcuDpiYXzpnL46bWFpfOmdkjpxaW5nfOmd" +
            "mTpqaW5nfOmdnjpmZWl86Z2gOmthb3zpnaE6bWl86Z2iOm1pYW586Z2pOmdlfOmdtDp4dWV86Z22OmJhfOmeizp4aWV86Z6gOmp1fOmerTpiaWFu" +
            "fOmfpzpyZW586Z+zOnlpbnzpn7U6eXVufOmhmDp5dWFufOmhtTp5ZXzpobY6ZGluZ3zpobc6cWluZ3zpobk6eGlhbmd86aG6OnNodW586aG7Onh1" +
            "fOmhvTp3YW586aG+Omd1fOmhvzpkdW586aKCOnNvbmd86aKEOnl1fOmihTpsdXzpooY6bGluZ3zpooc6cG986aKIOmppbmd86aKQOnlpfOmikTpw" +
            "aW586aKTOnR1aXzpopQ6aGFufOmiljp5aW5nfOmilzprZXzpopg6dGl86aKaOmV86aKcOnlhbnzpop06ZXzpoqA6ZGlhbnzpoqQ6Y2hhbnzpo446" +
            "ZmVuZ3zpo5g6cGlhb3zpo546ZmVpfOmjnzpzaGl86aOoOnhpYW5nfOmkkDpjYW586aSuOnRpZXzppZU6dGFvfOmlpTpqaXzppao6cmVufOmlrTpm" +
            "YW586aWuOnlpbnzppbA6c2hpfOmlsTpiYW986aWyOnNpfOmltTplcnzppbY6cmFvfOmlvDpiaW5nfOmlvzplfOmmhjpndWFufOmmiDprdWl86aaP" +
            "OmxpdXzpppA6eGl1fOmmljpzaG91fOmmmTp4aWFuZ3zpqaw6bWF86amtOnl1fOmprjp0dW986amvOnh1bnzpqbA6Y2hpfOmpsTpxdXzpqbM6Ym98" +
            "6am0Omx2fOmptjpzaGl86am7OnpodXzpqb46amlhfOmpvzp5aXzpqoE6eGlhb3zpqoI6bWF86aqEOmppYW986aqHOmhhaXzpqos6Y2hlbmd86aqM" +
            "OnlhbnzpqpE6cWl86aqXOnBpYW586aqaOnNhb3zpqps6d3V86aqhOmx1b3zpqqQ6emhvdXzpqqg6Z3V86aqwOnRvdXzpqrc6a3V86aq4OmhhaXzp" +
            "qrw6Z2V86auFOmxvdXzpq5M6c3VpfOmrlDp0aXzpq5g6Z2FvfOmsvDpndWl86a2BOmt1aXzprYI6aHVufOmthDpwb3zprYU6bWVpfOmthzp5YW58" +
            "6a2JOmxpYW5nfOmtjTp3YW5nfOmtkTpjaGl86a2UOm1vfOmxvDp5dXzpsoE6bHV86bKcOnhpYW586bOEOmV86bONOnFpfOmznjpsaW586bifOm5p" +
            "YW986bihOmppfOm4ozptaW5nfOm4pTpvdXzpuKY6eWF86bitOnlhfOm4tTp0dW986bi/Omhvbmd86bmFOmV86bmJOnd1fOm5ijpxdWV86bmkOmhl" +
            "fOm5pjp5aW5nfOm5qzpqaXV86bmwOnlpbmd86bm/Omx1fOm6pjptYWl86bq7Om1hfOm7hDpodWFuZ3zpu446bGl86buPOm5pYW586buROmhlaXzp" +
            "u5g6bW986buvOmFufOm8kzpndXzpvKA6c2h1fOm8rDp5b3V86by7OmJpfOm8vjpoYW586b2QOnFpfOm9kTpqaXzpvb86Y2hpfOm+hDpsaW5nfOm+" +
            "mTpsb25n";

        public static bool Matches(string query, params string[] values)
        {
            string normalizedQuery = NormalizeQuery(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return true;
            }

            SearchIndex index = GetIndex(values);
            return MatchesNormalized(index, normalizedQuery);
        }

        public static PreparedSearch Prepare(params string[] values)
        {
            return new PreparedSearch(SearchIndex.Build(values ?? Array.Empty<string>()));
        }

        private static bool MatchesNormalized(SearchIndex index, string normalizedQuery)
        {
            string[] terms = normalizedQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return terms.All(index.MatchesTerm);
        }

        private static SearchIndex GetIndex(string[] values)
        {
            string key = string.Join("", values ?? Array.Empty<string>());
            if (IndexCache.TryGetValue(key, out SearchIndex cached))
            {
                return cached;
            }

            if (IndexCache.Count > MaxIndexCacheSize)
            {
                IndexCache.Clear();
            }

            SearchIndex index = SearchIndex.Build(values ?? Array.Empty<string>());
            IndexCache[key] = index;
            return index;
        }

        private static Dictionary<char, string> PinyinMap
        {
            get
            {
                if (_pinyinMap != null)
                {
                    return _pinyinMap;
                }

                Dictionary<char, string> map = new Dictionary<char, string>();
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(PinyinDataBase64));
                foreach (string pair in decoded.Split('|'))
                {
                    int split = pair.IndexOf(':');
                    if (split <= 0 || split >= pair.Length - 1)
                    {
                        continue;
                    }

                    map[pair[0]] = pair.Substring(split + 1);
                }

                _pinyinMap = map;
                return _pinyinMap;
            }
        }

        private static string NormalizeQuery(string value)
        {
            SearchText text = BuildSearchText(value ?? string.Empty);
            return string.IsNullOrWhiteSpace(text.RawWithSpaces) ? string.Empty : text.RawWithSpaces;
        }

        private static SearchText BuildSearchText(string value)
        {
            StringBuilder raw = new StringBuilder();
            StringBuilder compact = new StringBuilder();
            StringBuilder pinyin = new StringBuilder();
            StringBuilder pinyinCompact = new StringBuilder();
            StringBuilder initials = new StringBuilder();
            bool previousSpace = false;
            bool inTag = false;
            bool hasChinese = false;

            foreach (char ch in value ?? string.Empty)
            {
                if (ch == '<')
                {
                    inTag = true;
                    continue;
                }

                if (inTag)
                {
                    if (ch == '>')
                    {
                        inTag = false;
                    }

                    continue;
                }

                if (char.IsWhiteSpace(ch))
                {
                    AppendSpace(raw, ref previousSpace);
                    continue;
                }

                if (IsChinese(ch))
                {
                    hasChinese = true;
                    raw.Append(ch);
                    compact.Append(ch);
                    previousSpace = false;
                    if (PinyinMap.TryGetValue(ch, out string py) && !string.IsNullOrWhiteSpace(py))
                    {
                        pinyin.Append(py).Append(' ');
                        pinyinCompact.Append(py);
                        initials.Append(py[0]);
                    }
                    else
                    {
                        initials.Append(ch);
                    }

                    continue;
                }

                if (char.IsLetterOrDigit(ch))
                {
                    char lower = char.ToLowerInvariant(ch);
                    raw.Append(lower);
                    compact.Append(lower);
                    pinyin.Append(lower);
                    pinyinCompact.Append(lower);
                    previousSpace = false;
                }
            }

            return new SearchText(
                raw.ToString().Trim(),
                compact.ToString(),
                pinyin.ToString().Trim(),
                pinyinCompact.ToString(),
                initials.ToString(),
                hasChinese);
        }

        private static void AppendSpace(StringBuilder builder, ref bool previousSpace)
        {
            if (builder.Length == 0 || previousSpace)
            {
                return;
            }

            builder.Append(' ');
            previousSpace = true;
        }

        private static bool IsChinese(char ch)
        {
            return ch >= '一' && ch <= '鿿';
        }

        private readonly struct SearchText
        {
            public SearchText(string rawWithSpaces, string compact, string pinyinWithSpaces, string pinyinCompact, string initials, bool hasChinese)
            {
                RawWithSpaces = rawWithSpaces ?? string.Empty;
                Compact = compact ?? string.Empty;
                PinyinWithSpaces = pinyinWithSpaces ?? string.Empty;
                PinyinCompact = pinyinCompact ?? string.Empty;
                Initials = initials ?? string.Empty;
                HasChinese = hasChinese;
            }

            public string RawWithSpaces { get; }
            public string Compact { get; }
            public string PinyinWithSpaces { get; }
            public string PinyinCompact { get; }
            public string Initials { get; }
            public bool HasChinese { get; }
        }

        internal sealed class SearchIndex
        {
            private readonly string _raw;
            private readonly string _compact;
            private readonly string _pinyin;
            private readonly string _pinyinCompact;
            private readonly string _initials;

            private SearchIndex(string raw, string compact, string pinyin, string pinyinCompact, string initials)
            {
                _raw = raw ?? string.Empty;
                _compact = compact ?? string.Empty;
                _pinyin = pinyin ?? string.Empty;
                _pinyinCompact = pinyinCompact ?? string.Empty;
                _initials = initials ?? string.Empty;
            }

            public static SearchIndex Build(IEnumerable<string> values)
            {
                StringBuilder raw = new StringBuilder();
                StringBuilder compact = new StringBuilder();
                StringBuilder pinyin = new StringBuilder();
                StringBuilder pinyinCompact = new StringBuilder();
                StringBuilder initials = new StringBuilder();

                foreach (string value in values ?? Array.Empty<string>())
                {
                    SearchText part = BuildSearchText(value ?? string.Empty);
                    AppendPart(raw, part.RawWithSpaces);
                    AppendPart(compact, part.Compact);
                    AppendPart(pinyin, part.PinyinWithSpaces);
                    AppendPart(pinyinCompact, part.PinyinCompact);
                    AppendPart(initials, part.Initials);
                }

                return new SearchIndex(
                    raw.ToString(),
                    compact.ToString(),
                    pinyin.ToString(),
                    pinyinCompact.ToString(),
                    initials.ToString());
            }

            public bool MatchesTerm(string term)
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    return true;
                }

                SearchText query = BuildSearchText(term);
                string raw = query.RawWithSpaces;
                string compact = query.Compact;
                string pinyinCompact = query.PinyinCompact;
                string initials = query.Initials;
                bool queryHasChinese = query.HasChinese;

                if (Contains(_raw, raw) || Contains(_compact, compact) || Contains(_pinyinCompact, compact))
                {
                    return true;
                }

                if (!queryHasChinese && Contains(_initials, compact))
                {
                    return true;
                }

                if (!string.Equals(compact, pinyinCompact, StringComparison.Ordinal) &&
                    Contains(_pinyinCompact, pinyinCompact))
                {
                    return true;
                }

                return !queryHasChinese && Contains(_initials, initials);
            }

            private static bool Contains(string haystack, string needle)
            {
                return !string.IsNullOrEmpty(needle) &&
                    !string.IsNullOrEmpty(haystack) &&
                    haystack.IndexOf(needle, StringComparison.Ordinal) >= 0;
            }

            private static void AppendPart(StringBuilder builder, string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(value);
            }
        }

        internal sealed class PreparedSearch
        {
            private readonly SearchIndex _index;

            internal PreparedSearch(SearchIndex index)
            {
                _index = index;
            }

            public bool Matches(string query)
            {
                string normalizedQuery = NormalizeQuery(query);
                return string.IsNullOrWhiteSpace(normalizedQuery) || MatchesNormalized(_index, normalizedQuery);
            }
        }
    }
}

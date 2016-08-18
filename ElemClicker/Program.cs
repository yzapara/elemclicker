using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using OpenQA.Selenium.Chrome;

namespace ElemClicker
{
  class Program
  {
    static void Main(string[] args)
    {
      string login, pwd;
      double coeff;
      int timeoutMsec, numrRepeats;
      if (args.Length < 5)
      {
        //coeff = 1.03;
        //timeoutMsec = 3600000;
        //numrRepeats = 240;
        Console.WriteLine("Set parameters in such order: login password coeff numrRepeats timeout");
        Console.WriteLine("*login password - login and password to the game");
        Console.WriteLine("*coeff max exceed of duel opponent in %.");
        Console.WriteLine("*numrRepeats - how much times soft will repeat actions");
        Console.WriteLine("*timeout - pause between actions. Recommended value is 3600000 (1 hour)");
        Console.Read();
      }
      else
      {
        login = args[0];
        pwd = args[1];
        coeff = 1 + Convert.ToInt32(args[2]) / 100.0;
        numrRepeats = Convert.ToInt32(args[3]);
        timeoutMsec = Convert.ToInt32(args[4]);

        if (!Directory.Exists(@"Logs"))
          Directory.CreateDirectory("Logs");

        // Initialize the Chrome Driver
        for (var i = 0; i < numrRepeats; i++)
        {
          Console.WriteLine("[{0}] Start itteration #{1}. Pause between itterations {2} msc ", DateTime.Now, i, timeoutMsec);
          using (var driver = new ChromeDriver())
          {
            LoginToGame(driver, login, pwd);
            BeatBoss(driver, 7);
            BeatBoss(driver, 6);
            BeatBoss(driver, 5);
            BeatBoss(driver, 4);

            RunDuels(driver, coeff);

            //RunArenas(driver);
          }
          Console.WriteLine("[{0}] End itteration {1}. Pause between itterations {2} msc ", DateTime.Now, i, timeoutMsec);
          Thread.Sleep(timeoutMsec);
        }
      }
    }

    private static void LoginToGame(ChromeDriver driver, string login, string pass)
    {
      // Go to the home page
      driver.Navigate().GoToUrl("http://elem.mobi");

      var inButton = driver.FindElementsByClassName("be");
      inButton.First().Click();

      var loginField = driver.FindElementByName("plogin");
      var userPasswordField = driver.FindElementByName("ppass");

      loginField.SendKeys(login);
      userPasswordField.SendKeys(pass);

      var loginButton = driver.FindElementsByClassName("be");
      loginButton.First().Click();
    }

    private static string GetLogFileName(string pref, string end, string subfolder)
    {
      if (!Directory.Exists(string.Format(@"Logs\{0}", subfolder)))
        Directory.CreateDirectory(string.Format(@"Logs\{0}", subfolder));

      return string.Format(@"Logs\{9}\{0}_{1}_{2}_{3}_{4}_{5}_{6}_{7}.{8}",
          pref, DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, end, subfolder);
    }

    private static void RunDuels(ChromeDriver driver, double coeff)
    {
      const int numAttempts = 10;

      var results = new List<string>(0) { "Started at " + DateTime.Now };
      Console.WriteLine(string.Format("[{0}] Start duels", DateTime.Now));
      Console.WriteLine(string.Format("[{0}] Select opponent in range +-{1}% ", DateTime.Now, coeff - 1));

      for (var i = 0; i < numAttempts; i++)
      {
        var beatres = RunDuelOnce(driver, coeff);
        if (beatres.Count == 0)
        {
          results.Add("No availible duels");
          Console.WriteLine(string.Format("[{0}] No availible duels", DateTime.Now));
          break;
        }
        results.AddRange(beatres);
      }

      results.Add(string.Format("Finished at {0} in {1} attempts", DateTime.Now, results.Count));
      Console.WriteLine(string.Format("[{0}] End of duels", DateTime.Now));

      string fileName = GetLogFileName("duel_", "txt", "Duels");
      File.WriteAllLines(fileName, results);
    }

    private static List<string> RunDuelOnce(ChromeDriver driver, double coeff)
    {
      driver.Navigate().GoToUrl("http://elem.mobi/duel/");

      var result = new List<string>();

      try
      {
        #region Select opponent
        var myInitialHealthElem = driver.FindElementsByClassName("c_da");
        int myHealth;
        if (myInitialHealthElem.Count > 0)
          myHealth = int.Parse(myInitialHealthElem.First().Text.Replace(" ", ""));
        else throw new Exception("Can't defime my health");

        for (int i = 0; i < 20; i++)
        {
          Thread.Sleep(200);
          var startDuelButton = driver.FindElementsByClassName("be");
          if (startDuelButton.Count > 0)
            if (startDuelButton.First().Text == "Напасть")
            {
              var oponnentHealthElem = driver.FindElementsByClassName("mr5");
              int oponnentHealth;
              if (oponnentHealthElem.Count > 0)
                oponnentHealth = int.Parse(oponnentHealthElem.First().Text.Replace(" ", ""));
              else throw new Exception("Can't defime opponent health");

              if (((myHealth * coeff) >= oponnentHealth) && (myHealth * (3 - 2 * coeff)) <= oponnentHealth)
              {
                result.Add(string.Format("Select opponent with {0} health", oponnentHealth));
                Console.WriteLine(string.Format("[{0}] Select opponent with {1} health", DateTime.Now, oponnentHealth));
                startDuelButton.First().Click();
                break;
              }
              result.Add(string.Format("Skip opponent with {0} health", oponnentHealth));
              Console.WriteLine(string.Format("[{0}] Skip opponent with {1} health.", DateTime.Now, oponnentHealth));
              startDuelButton[1].Click();
            }
            else
            {
              result.Clear();
              return result;
            }
        }
        #endregion

        #region Run duel
        for (; ; )
        {
          Thread.Sleep(200);
          var cards = driver.FindElementsByClassName("card");
          if (cards.Count < 6)
          {
            var startDuelButton = driver.FindElementsByClassName("be");
            if (startDuelButton.Count > 0)
              if (startDuelButton.First().Text == "Еще дуэль")
              {
                var duelResult = driver.FindElementsByClassName("mb5");
                if (duelResult.Count > 0)
                {
                  var res = duelResult.First().Text;
                  if (!res.StartsWith("— Победа —"))
                  {
                    res = "— Поражение — " + res;
                    driver.GetScreenshot().SaveAsFile(GetLogFileName("lostduel_", "jpg", "LostDuel"), ImageFormat.Jpeg);
                  }
                  result.Add(string.Format("{0}", res));
                  Console.WriteLine(string.Format("[{0}] Duel result: {1}", DateTime.Now, res));
                }

                var duelResult2 = driver.FindElementsByClassName("c_99");
                if (duelResult2.Count > 0)
                {
                  result.Add(string.Format("{0}", duelResult2.First().Text));
                  Console.WriteLine(string.Format("[{0}] Duel result: {1}", DateTime.Now, duelResult2.First().Text));
                }
                Console.WriteLine("=====================================================");
                return result;
              }
          }

          var dmg = driver.FindElementsByClassName("mb5");
          if (dmg.Count < 3)
            throw new Exception("No dmg elements found");

          var cardsEffect = new List<int>(6);

          for (int i = 0; i < 6; i++)
          {
            cardsEffect.Add(int.Parse(cards[i].Text.Replace(" ", "")));
          }

          var dmgMult = new List<double>(3);

          var attackEffect = new List<double>(3) { 0.0, 0.0, 0.0 };

          for (int i = 0; i < 3; i++)
          {
            var dmgTxt = dmg[i].Text.Substring(2);
            switch (dmgTxt)
            {
              case "0.5": { dmgMult.Add(0.5); break; }
              case "1": { dmgMult.Add(1); break; }
              case "1.5": { dmgMult.Add(1.5); break; }
            }
          }

          attackEffect[0] = cardsEffect[1] * dmgMult[0] - cardsEffect[0] * (2 - dmgMult[0]);
          attackEffect[1] = cardsEffect[3] * dmgMult[1] - cardsEffect[2] * (2 - dmgMult[1]);
          attackEffect[2] = cardsEffect[5] * dmgMult[2] - cardsEffect[4] * (2 - dmgMult[2]);
          //Console.WriteLine(string.Format("[{0}] Множители по картам {1}, {2}, {3}", DateTime.Now, dmgMult[0], dmgMult[1], dmgMult[2]));

          if ((attackEffect[0] >= attackEffect[1]) && (attackEffect[0] >= attackEffect[2]))
          {
            result.Add(string.Format("Beat on {0}", attackEffect[0]));
            Console.WriteLine(string.Format("[{0}] beat by card #1 on {1} against {2}. Effect {3}", DateTime.Now, cardsEffect[1] * dmgMult[0], cardsEffect[0] * (2 - dmgMult[0]), attackEffect[0]));
            cards[1].Click();
          }
          else if ((attackEffect[1] >= attackEffect[0]) && (attackEffect[1] >= attackEffect[2]))
          {
            result.Add(string.Format("Beat on {0}", attackEffect[1]));
            Console.WriteLine(string.Format("[{0}] beat by card #2 on {1} against {2}. Effect {3}", DateTime.Now, cardsEffect[3] * dmgMult[1], cardsEffect[2] * (2 - dmgMult[1]), attackEffect[1]));
            cards[3].Click();
          }
          else
          {
            result.Add(string.Format("Beat on {0}", attackEffect[2]));
            Console.WriteLine(string.Format("[{0}] beat by card #3 on {1} against {2}. Effect {3}", DateTime.Now, cardsEffect[5] * dmgMult[2], cardsEffect[4] * (2 - dmgMult[2]), attackEffect[2]));
            cards[5].Click();
          }
        }

        #endregion
      }
      catch (Exception e)
      {
        result.Add(string.Format("Error: {0}", e.Message));
      }

      return result;
    }

    private static void BeatBoss(ChromeDriver driver, int bossNum)
    {
      const int numAttempts = 1000;

      var results = new List<string>(0) { string.Format("Start beat boss {0} at {1}", bossNum, DateTime.Now) };
      Console.WriteLine(string.Format("[{0}] Start beat boss {1}", DateTime.Now, bossNum));

      for (var i = 0; i < numAttempts; i++)
      {
        var beatres = BeatBossOnce(driver, bossNum);
        Console.WriteLine(string.Format("[{0}] {1} ", DateTime.Now, beatres));
        results.Add(beatres);
        if ((beatres == "WIN") || (beatres == "Boss not ready now") || (beatres.StartsWith("ERROR")))
        {
          if ((beatres == "WIN"))
            driver.GetScreenshot().SaveAsFile(GetLogFileName(string.Format("boss{0}_", bossNum), "jpg", "Boss" + bossNum.ToString()), ImageFormat.Jpeg);
          break;
        }
      }

      results.Add(string.Format("Finished fight with boss {0} in {1} attempts", DateTime.Now, results.Count - 2));
      Console.WriteLine(string.Format("[{0}] Finished fight with boss {1} in {2} attempts", DateTime.Now, bossNum, results.Count - 2));

      var fileName = GetLogFileName(string.Format("boss{0}_", bossNum), "txt", "Boss" + bossNum.ToString());
      File.WriteAllLines(fileName, results);
    }

    private static string BeatBossOnce(ChromeDriver driver, int bossNum)
    {
      driver.Navigate().GoToUrl(string.Format("http://elem.mobi/dungeon/{0}/start/", bossNum));

      for (int i = 0; i < 20; i++)
      {
        try
        {
          Thread.Sleep(250);
          var cards = driver.FindElementsByClassName("card");
          if (cards.Count > 2)
            cards[1].Click();
          else
          {
            var resetButton = driver.FindElementsByClassName("be");
            if (resetButton.Count > 0)
              if (resetButton.First().Text == "Начать снова")
              {
                var remainingHealth = driver.FindElementsByClassName("wlttl");
                if (remainingHealth.Count > 0)
                  return string.Format("Can't beat in {0} attempts. Boss health is {1}", i, remainingHealth[1].Text);
              }
              else
              {
                if (resetButton.First().Text == "Испытания")
                  return "Boss not ready now";
                return "WIN";
              }
            else if (cards.Count == 0) //no battle availible at the moment
              return "Boss not ready now";
            return "WIN";
          }
        }
        catch (Exception e)
        {
          return "ERROR: " + e.Message;
        }
      }
      return "Boss not ready now";
    }

    //private static void RunArenas(ChromeDriver driver)
    //{
    //    const int numAttempts = 10;

    //    var results = new List<string>(0) { "Started at " + DateTime.Now };
    //    Console.WriteLine(string.Format("[{0}] Начало арен", DateTime.Now));
    //    //Console.WriteLine(string.Format("[{0}] Выбираем оппонента для дуэли сильнее не более чем на {1}%", DateTime.Now, coeff - 1));

    //    for (var i = 0; i < numAttempts; i++)
    //    {
    //        var beatres = RunArenaOnce(driver);
    //        //if (beatres.Count == 0)
    //        //{
    //        //    results.Add("Кончились доступные дуэли");
    //        //    Console.WriteLine(string.Format("[{0}] Кончились доступные дуэли", DateTime.Now));
    //        //    break;
    //        //}
    //        results.AddRange(beatres);
    //    }

    //    results.Add(string.Format("Finished at {0} in {1} attempts", DateTime.Now, results.Count));
    //    Console.WriteLine(string.Format("[{0}] Конец арен", DateTime.Now));

    //    string fileName = GetLogFileName("arena_", "txt");
    //    File.WriteAllLines(fileName, results);
    //}

    //private static List<string> RunArenaOnce(ChromeDriver driver)
    //{
    //    driver.Navigate().GoToUrl("http://elem.mobi/arena/");

    //    var result = new List<string>();

    //    try
    //    {
    //        #region Participate to arena
    //        //var myInitialHealthElem = driver.FindElementsByClassName("c_da");
    //        //int myHealth;
    //        //if (myInitialHealthElem.Count > 0)
    //        //    myHealth = int.Parse(myInitialHealthElem.First().Text.Replace(" ", ""));
    //        //else throw new Exception("Can't defime my health");

    //        for (;;)
    //        {
    //            Thread.Sleep(1000);
    //            var participateArena = driver.FindElementsByClassName("be");
    //            if (participateArena.Count > 0)
    //                if ((participateArena.First().Text == "Записаться") ||
    //                    (participateArena.First().Text == "Обновить"))
    //                    participateArena.First().Click();
    //                else
    //                    break;

    //        }
    //        #endregion

    //        #region Play arena
    //        for (;;)
    //        {
    //            //Thread.Sleep(150);
    //            var cards = driver.FindElementsByClassName("card");

    //            if(cards.Count == 0)
    //            {
    //                driver.GetScreenshot().SaveAsFile(GetLogFileName("arena_", "jpg"), ImageFormat.Jpeg);
    //                var arenaResult = driver.FindElementsByClassName("wr7");
    //                if (arenaResult.Count > 0)
    //                    result.Add(arenaResult.First().Text);
    //                return result;
    //            }

    //            var numOfEnemies = Convert.ToInt32(driver.FindElementByXPath("/html/body/div[1]/text()").Text.TrimStart().TrimEnd().Substring(7,1));

    //            var opponents = new List<Opponent>(numOfEnemies);

    //            var enemyName = driver.FindElementByXPath("/html/body/div[2]/div/div/div[1]/span/text()");
    //            var enemyHealth = driver.FindElementByXPath("/html/body/div[2]/div/div/div[1]/div/text()");


    //            opponents.Add(new Opponent() { Name = enemyName.Text, Health = Convert.ToInt32(enemyHealth.Text.TrimEnd()) });

    //                                   //var dmg = driver.FindElementsByClassName("mb5");
    //                //if (dmg.Count < 3)
    //                //    throw new Exception("No dmg elements found");

    //                //var cardsEffect = new List<int>(6);

    //                //for (int i = 0; i < 6; i++)
    //                //{
    //                //    cardsEffect.Add(int.Parse(cards[i].Text.Replace(" ", "")));
    //                //}

    //                //var dmgMult = new List<double>(3);

    //                //var attackEffect = new List<double>(3) { 0.0, 0.0, 0.0 };

    //                //for (int i = 0; i < 3; i++)
    //                //{
    //                //    var dmgTxt = dmg[i].Text.Substring(2);
    //                //    switch (dmgTxt)
    //                //    {
    //                //        case "0.5": { dmgMult.Add(0.5); break; }
    //                //        case "1": { dmgMult.Add(1); break; }
    //                //        case "1.5": { dmgMult.Add(1.5); break; }
    //                //    }
    //                //}

    //                //attackEffect[0] = cardsEffect[1] * dmgMult[0] - cardsEffect[0] * (2 - dmgMult[0]);
    //                //attackEffect[1] = cardsEffect[3] * dmgMult[1] - cardsEffect[2] * (2 - dmgMult[1]);
    //                //attackEffect[2] = cardsEffect[5] * dmgMult[2] - cardsEffect[4] * (2 - dmgMult[2]);
    //                ////Console.WriteLine(string.Format("[{0}] Множители по картам {1}, {2}, {3}", DateTime.Now, dmgMult[0], dmgMult[1], dmgMult[2]));

    //                //if ((attackEffect[0] >= attackEffect[1]) && (attackEffect[0] >= attackEffect[2]))
    //                //{
    //                //    result.Add(string.Format("Beat on {0}", attackEffect[0]));
    //                //    Console.WriteLine(string.Format("[{0}] Удар картой 1 на {1} против {2}. Эффект {3}", DateTime.Now, cardsEffect[1] * dmgMult[0], cardsEffect[0] * (2 - dmgMult[0]), attackEffect[0]));
    //                //    cards[1].Click();
    //                //}
    //                //else if ((attackEffect[1] >= attackEffect[0]) && (attackEffect[1] >= attackEffect[2]))
    //                //{
    //                //    result.Add(string.Format("Beat on {0}", attackEffect[1]));
    //                //    Console.WriteLine(string.Format("[{0}] Удар картой 2 на {1} против {2}. Эффект {3}", DateTime.Now, cardsEffect[3] * dmgMult[1], cardsEffect[2] * (2 - dmgMult[1]), attackEffect[1]));
    //                //    cards[3].Click();
    //                //}
    //                //else
    //                //{
    //                //    result.Add(string.Format("Beat on {0}", attackEffect[2]));
    //                //    Console.WriteLine(string.Format("[{0}] Удар картой 3 на {1} против {2}. Эффект {3}", DateTime.Now, cardsEffect[5] * dmgMult[2], cardsEffect[4] * (2 - dmgMult[2]), attackEffect[2]));
    //                //    cards[5].Click();
    //                //}
    //        }

    //        #endregion
    //    }
    //    catch (Exception e)
    //    {
    //        result.Add(string.Format("Error: {0}", e.Message));
    //    }

    //    return result;
    //}
  }

  public class Opponent
  {
    public string Name { get; set; }
    public int Health { get; set; }
    public List<int> Cards { get; set; }
    public List<double> AttackEffect { get; set; }
    public List<double> DmgCoeffs { get; set; }
  }
}


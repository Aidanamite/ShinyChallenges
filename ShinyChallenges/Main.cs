using HarmonyLib;
using SRML;
using SRML.SR;
using SRML.Console;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Challenges;
using Shinies;
using Console = SRML.Console.Console;
using Object = UnityEngine.Object;

namespace ShinyChallenges
{
    public class Main : ModEntryPoint
    {
        internal static Assembly modAssembly = Assembly.GetExecutingAssembly();
        internal static string modName = $"{modAssembly.GetName().Name}";
        internal static string modDir = $"{System.Environment.CurrentDirectory}\\SRML\\Mods\\{modName}";

        public override void PreLoad()
        {
            TranslationPatcher.AddPediaTranslation("challenge.onlyshinyplorts.name", "Shiny Plorts Only");
            TranslationPatcher.AddPediaTranslation("challenge.onlyshinyplorts.desc", "Only shiny slimes will produce after eating. Normal slimes will still eat but will not produce anything");
            TranslationPatcher.AddPediaTranslation("challenge.bettershinies.name", "Better Shinies");
            TranslationPatcher.AddPediaTranslation("challenge.bettershinies.desc", "Shiny slimes produce double what normal slimes do");
            HarmonyInstance.PatchAll();
        }
        public override void Load()
        {
            new Challenge("onlyShinyPlorts",
                "challenge.onlyshinyplorts.name",
                "challenge.onlyshinyplorts.desc",
                GameContext.Instance.SlimeDefinitions.GetSlimeByIdentifiableId(Identifiable.Id.GOLD_SLIME).AppearancesDefault[0].Icon,
                Challenge.ChallengeType.Bad);
            var icon = GameContext.Instance.LookupDirector.GetIcon(Identifiable.Id.GOLD_PLORT).texture.GetReadable();
            var icon2 = LoadImage("iconOverlay.png",512,512);
            icon.ModifyTexturePixels(icon2.Overlay);
            new Challenge("betterShinies",
                "challenge.bettershinies.name",
                "challenge.bettershinies.desc",
                icon.CreateSprite(),
                Challenge.ChallengeType.Good);
        }
        public static void Log(string message) => Console.Log($"[{modName}]: " + message);
        public static void LogError(string message) => Console.LogError($"[{modName}]: " + message);
        public static void LogWarning(string message) => Console.LogWarning($"[{modName}]: " + message);
        public static void LogSuccess(string message) => Console.LogSuccess($"[{modName}]: " + message);

        public static Texture2D LoadImage(string filename, int width) => LoadImage(filename, width, width);

        public static Texture2D LoadImage(string filename, int width, int height)
        {
            var a = modAssembly;
            var spriteData = a.GetManifestResourceStream(a.GetName().Name + "." + filename);
            if (spriteData == null)
            {
                LogError(filename + " does not exist in the assembly");
                return null;
            }
            var rawData = new byte[spriteData.Length];
            spriteData.Read(rawData, 0, rawData.Length);
            var tex = new Texture2D(width, height);
            tex.LoadImage(rawData);
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
    }

    [HarmonyPatch(typeof(SlimeEat),"EatAndProduce")]
    class Patch_SlimeEat
    {
        public static SlimeEat calling = null;
        static void Prefix(SlimeEat __instance) => calling = __instance;
        static void Postfix() => calling = null;
    }

    [HarmonyPatch(typeof(SlimeDiet.EatMapEntry),"NumToProduce")]
    class Patch_EatMapEntry
    {
        static void Postfix(ref int __result)
        {
            if (!Patch_SlimeEat.calling)
                return;
            var flag = Patch_SlimeEat.calling.GetComponent<ShinySpawn>() && Patch_SlimeEat.calling.GetComponent<ShinySpawn>().skin != ShinySpawn.Skin.Normal;
            if (flag && Challenge.IsActive("betterShinies"))
                __result *= 2;
            if (!flag && Challenge.IsActive("onlyShinyPlorts"))
                __result = 0;
        }
    }

    [HarmonyPatch(typeof(SlimeEatWater), "Produce")]
    class Patch_SlimeEatWater
    {
        static void Prefix(SlimeEatWater __instance, ref int count)
        {
            var flag = __instance.GetComponent<ShinySpawn>() && __instance.GetComponent<ShinySpawn>().skin != ShinySpawn.Skin.Normal;
            if (flag && Challenge.IsActive("betterShinies"))
                count *= 2;
            if (!flag && Challenge.IsActive("onlyShinyPlorts"))
                count = 0;
        }
    }

    [HarmonyPatch(typeof(SlimeEatAsh), "ProduceAfterDelay")]
    class Patch_SlimeEatAsh
    {
        static void Prefix(SlimeEatAsh __instance, ref int count)
        {
            var flag = __instance.GetComponent<ShinySpawn>() && __instance.GetComponent<ShinySpawn>().skin != ShinySpawn.Skin.Normal;
            if (flag && Challenge.IsActive("betterShinies"))
                count *= 2;
            if (!flag && Challenge.IsActive("onlyShinyPlorts"))
                count = 0;
        }
    }

    static class ExtentionMethods
    {
        public static Sprite GetReadable(this Sprite source)
        {
            return Sprite.Create(source.texture.GetReadable(), source.rect, source.pivot, source.pixelsPerUnit);
        }

        public static Texture2D GetReadable(this Texture2D source)
        {
            RenderTexture temp = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, temp);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = temp;
            Texture2D texture = new Texture2D(source.width, source.height);
            texture.ReadPixels(new Rect(0, 0, temp.width, temp.height), 0, 0);
            texture.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(temp);
            return texture;
        }

        public static Sprite CreateSprite(this Texture2D texture) => Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1);

        public static void ModifyTexturePixels(this Texture2D texture, Func<Color, Color> colorChange)
        {
                var p = texture.GetPixels();
                for (int x = 0; x < p.Length; x++)
                    p[x] = colorChange(p[x]);
                texture.SetPixels(p);
            texture.Apply(true);
        }

        public static void ModifyTexturePixels(this Texture2D texture, Func<Color, float, float, Color> colorChange)
        {
            var p = texture.GetPixels();
            for (int x = 0; x < p.Length; x++)
                p[x] = colorChange(p[x], (x % texture.width + 1) / (float)texture.width, (x / texture.width + 1) / (float)texture.height);
            texture.SetPixels(p);
            texture.Apply(true);
        }

        public static Color Overlay(this Texture2D t, Color c, float u, float v)
        {
            var c2 = t.GetPixelBilinear(u, v);
            return new Color(c.r * (1 - c2.a) + c2.r * c2.a, c.g * (1 - c2.a) + c2.g * c2.a, c.b * (1 - c2.a) + c2.b * c2.a, Mathf.Max(c.a, c2.a));
        }
    }
}
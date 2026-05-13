using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CutsceneManager : MonoBehaviour
{
    // Реализует Singleton — доступ к менеджеру через CutsceneManager.Instance
    public static CutsceneManager Instance;

    // Сериализуемый список для настройки пары "ключ -> объект катсцены" в инспекторе
    [SerializeField] private List<CutsceneStruct> cutscenes = new List<CutsceneStruct>();

    // База катсцен: словарь для быстрого доступа по ключу к объекту катсцены
    public static Dictionary<string, GameObject> cutsceneDataBase = new Dictionary<string, GameObject>();

    // Текущая активная катсцена (или null, если ничего не воспроизводится)
    public static GameObject activeCutscene;

    private void Awake()
    {
        // Инициализация Singleton (защита от дублирования)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Заполнить базу катсцен из списка
        InitializeCutsceneDataBase();

        // Отключить все объекты катсцен при старте
        foreach (var cutscene in cutsceneDataBase)
        {
            if (cutscene.Value != null)
                cutscene.Value.SetActive(false);
        }
    }

    // ����� � ������� �� ��������� Dictionary cutsceneDataBase
    private void InitializeCutsceneDataBase()
    {
        // Очистить базу перед заполнением
        cutsceneDataBase.Clear();

        // Заполнить словарь из списка `cutscenes`
        for (int i = 0; i < cutscenes.Count; i++)
        {
            var entry = cutscenes[i];
            if (string.IsNullOrEmpty(entry.cutsceneKey) || entry.cutsceneObject == null)
                continue;

            if (!cutsceneDataBase.ContainsKey(entry.cutsceneKey))
                cutsceneDataBase.Add(entry.cutsceneKey, entry.cutsceneObject);
            else
                Debug.LogWarning($"Ключ катсцены '{entry.cutsceneKey}' уже существует в базе.");
        }
    }

    // ����� ��� ��������� �������� �� �����
    public void StartCutscene(string cutsceneKey)
    {
        // Получить катсцену из базы
        if (!cutsceneDataBase.TryGetValue(cutsceneKey, out var target))
        {
            Debug.LogError($"Катсцена с ключом \"{cutsceneKey}\" не найдена в базе.");
            return;
        }

        // Если уже воспроизводится та же катсцена — ничего не делаем
        if (activeCutscene != null && activeCutscene == target)
            return;

        // Установить активную катсцену
        activeCutscene = target;

        // Отключить все катсцены
        foreach (var cutscene in cutsceneDataBase)
        {
            if (cutscene.Value != null)
                cutscene.Value.SetActive(false);
        }

        // Включить выбранную катсцену
        target?.SetActive(true);
    }

    // ����� ������� ��������� ������� ��������
    public void EndCutscene()
    {
        // Остановить текущую катсцену и очистить ссылку
        if (activeCutscene != null)
        {
            activeCutscene.SetActive(false);
            activeCutscene = null;
        }
    }
}

// ��������� ������� ��� �����, ����� ����� ����������� ��� �������� � Key � Value � Dictionary cutsceneDataBase
[System.Serializable]
public struct CutsceneStruct
{
    public string cutsceneKey;
    public GameObject cutsceneObject;
}
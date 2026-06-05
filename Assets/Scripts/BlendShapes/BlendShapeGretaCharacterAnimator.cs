using animationparameters;
using audioElements;
using autodeskcharacter;
using BlendShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using thriftImpl;
using time;
using tools;
using UnityEngine;

public class BlendShapeGretaCharacterAnimator : MonoBehaviour
{
    static int NUMBER_OF_FAPS = Enum.GetNames(typeof(FAPType)).Length;
    static int NUMBER_OF_BAPS = 296;
    private List<autodeskcharacter.bapmapper.BapMapper> bapMappers;
    private ConcatenateJoints concatenator = new ConcatenateJoints();

    public bool sat = false;
    public float hipsDegrees = -90;
    public float kneesDegrees = 90;

    public bool distantConnection = true;
    public int FAP_RECEIVER_PORT = 9090;
    public int BAP_RECEIVER_PORT = 9900;
    public int AUDIO_RECEIVER_PORT = 9009;
    public int CMD_SENDER_PORT = 9912;
    public string GretaServersHost = "localhost";

    public float UpperBodyAnimationEffect = 0.9f;
    public bool agentPlaying;
    public bool animateAgent = true;
    public string animationID = "noIdea";
    public string animationIDold = "noIdea";

    public TimeController characterTimer;
    public bool thriftConsumerOpened = false;

    public AudioFilePlayer audioFilePlayer;
    public FAPReceiver fapReceiver;
    public BAPReceiver bapReceiver;
    public AudioReceiver audioReceiver;
    public CommandSender commandSender;

    private AnimationParametersFrame lastFAPFrame;
    private AnimationParametersFrame lastBAPFrame;
    private int cptFrames;

    private static int ANIM_DELAY = 000;

    private AudioSource _currentAudioSource = null;



    public class DirectModelBinding
    {
        public SkinnedMeshRenderer Mesh;
        public List<DirectBlendShapeMapping> Mappings;
        public List<(SkinnedMeshRenderer mesh, Dictionary<string, int> cache)> MeshCaches;
    }

    public DirectModelBinding DirectModel;

    public class ModelBinding
    {
        public SkinnedMeshRenderer Mesh;
        public List<BlendShapeMapping> Mappings;
        public List<(SkinnedMeshRenderer mesh, Dictionary<string, int> cache)> MeshCaches;
    }

    public ModelBinding Model;




    Dictionary<string, (string name, float weight)[]> blendshapeMapping;

    private List<(SkinnedMeshRenderer mesh, Dictionary<string, int> cache)> visemeMeshCaches;

    public UnityEngine.Object blendShapeProfile;
    public UnityEngine.Object visemeProfile;

    public bool DirectFapMapping = false;
    public bool directBlendShapeFromGreta = false;
    public bool phonemeMode = false;


    void Awake()
    {        
        // Temporary : fixes float errors format when communicating with greta (. vs ,)
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        string BlendShapeProfilePath = Application.streamingAssetsPath + "/" + blendShapeProfile.name + ".ini";
        blendshapeMapping = BlendShapes.VisemeProfileFileReader.FileReader(Application.streamingAssetsPath + "/" + visemeProfile.name + ".ini");
        Time.fixedDeltaTime = (float)0.04;
        characterTimer = new TimeController();
        characterTimer.setTimeMillis(0);

        bapReceiver = new BAPReceiver(NUMBER_OF_BAPS, BAP_RECEIVER_PORT);
        fapReceiver = new FAPReceiver(NUMBER_OF_FAPS + 1, FAP_RECEIVER_PORT);
        audioReceiver = new AudioReceiver(AUDIO_RECEIVER_PORT);

        lastFAPFrame = new AnimationParametersFrame(NUMBER_OF_FAPS + 1, 0);
        lastBAPFrame = new AnimationParametersFrame(NUMBER_OF_BAPS, 0);
        cptFrames = 0;
        commandSender = new CommandSender(GretaServersHost, CMD_SENDER_PORT);
        agentPlaying = false;

        audioFilePlayer = new AudioFilePlayer();
        _currentAudioSource = gameObject.AddComponent<AudioSource>();


        if (!phonemeMode)
        {
            if (DirectFapMapping)
            {
                var directProfile = DirectFapProfileFilReader.FileReader(
                Application.streamingAssetsPath + "/" + blendShapeProfile.name + ".ini");

                DirectModel = new DirectModelBinding();
                DirectModel.Mappings = new List<DirectBlendShapeMapping>();
                foreach (var kvp in directProfile)
                    DirectModel.Mappings.Add(new DirectBlendShapeMapping(kvp.Key, kvp.Value));

                var allMeshes = GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var allCaches = new List<(SkinnedMeshRenderer mesh, Dictionary<string, int> cache)>();
                foreach (var mesh in allMeshes)
                {
                    if (mesh == null || mesh.sharedMesh == null || mesh.sharedMesh.blendShapeCount == 0) continue;
                    var cache = new Dictionary<string, int>();
                    foreach (var mapping in DirectModel.Mappings)
                    {
                        if (cache.ContainsKey(mapping.blendShapeName)) continue;
                        cache[mapping.blendShapeName] = mesh.sharedMesh.GetBlendShapeIndex(mapping.blendShapeName);
                    }
                    allCaches.Add((mesh, cache));
                }
                DirectModel.MeshCaches = allCaches;
            }
            else
            {
                Model = new ModelBinding();
                Model.Mappings = BlendShapeMappingProfile.BlendShapeMappingBuild(BlendShapeProfileFileReader.FileReader(BlendShapeProfilePath));
                var allMeshes = GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var allCaches = new List<(SkinnedMeshRenderer mesh, Dictionary<string, int> cache)>();

                foreach (var mesh in allMeshes)
                {
                    if (mesh == null || mesh.sharedMesh == null || mesh.sharedMesh.blendShapeCount == 0) continue;
                    Model.Mesh = mesh;
                    var cache = MeshDataToDict(Model);
                    allCaches.Add((mesh, cache));
                }
                Model.MeshCaches = allCaches;
            }
        }
        visemeMeshCaches = new List<(SkinnedMeshRenderer mesh, Dictionary<string, int> cache)>();

        var allMeshesForBlendshapes = GetComponentsInChildren<SkinnedMeshRenderer>(true);

        foreach (var mesh in allMeshesForBlendshapes)
        {
            if (mesh == null || mesh.sharedMesh == null || mesh.sharedMesh.blendShapeCount == 0)
                continue;

            var cache = new Dictionary<string, int>();

            foreach (var kvp in blendshapeMapping)
            {
                foreach (var (bsName, _) in kvp.Value)
                {
                    if (cache.ContainsKey(bsName)) continue;
                    int idx = mesh.sharedMesh.GetBlendShapeIndex(bsName);
                    if (idx >= 0)
                        cache[bsName] = idx;
                }
            }

            if (cache.Count > 0)
                visemeMeshCaches.Add((mesh, cache));
        }

    }

    public void FixedUpdate()
    {
        if (animateAgent)
        {
            AnimationParametersFrame currentFAPFrame = null;
            AnimationParametersFrame currentBAPFrame = null;

            AudioElement currentAudio = null;

            if (distantConnection)
            {
                if (!thriftConsumerOpened)
                {

                    if (!fapReceiver.isConnected() && !fapReceiver.isOnConnection())
                    {
                        fapReceiver.startConnection();
                    }
                    else if (!bapReceiver.isConnected() && !bapReceiver.isOnConnection() && fapReceiver.isConnected())
                    {
                        Debug.Log("FAP Receiver started");
                        bapReceiver.startConnection();
                    }
                    else if (!audioReceiver.isConnected() && !audioReceiver.isOnConnection() && bapReceiver.isConnected())
                    {
                        Debug.Log("BAP Receiver started");
                        audioReceiver.startConnection();
                    }
                    else if (!commandSender.isConnected() && !commandSender.isOnConnection() && audioReceiver.isConnected())
                    {
                        Debug.Log("Audio Receiver started");
                        commandSender.startConnection();
                    }
                    else if (commandSender.isConnected())
                    {
                        Debug.Log("Connection Sender started");
                        thriftConsumerOpened = true;
                    }
                }
                else
                {
                    if (fapReceiver.timer.isSynchronized())
                    {
                        characterTimer.setTimeMillis(fapReceiver.timer.getTimeMillis() - ANIM_DELAY);
                        SceneManager.gretaClock = (float)characterTimer.getTimeMillis();
                        currentFAPFrame = fapReceiver.getCurrentFrame(characterTimer.getTimeMillis() / 40);
                        //Debug.Log(currentFAPFrame.AnimationParametersFrame2String(0));


                    }
                    if (bapReceiver.timer.isSynchronized())
                    {
                        if (SceneManager.gretaClock <= 0)
                        {
                            characterTimer.setTimeMillis(bapReceiver.timer.getTimeMillis() - ANIM_DELAY);
                            SceneManager.gretaClock = (float)(characterTimer.getTimeMillis());
                        }
                        currentBAPFrame = bapReceiver.getCurrentFrame(characterTimer.getTimeMillis() / 40);
                    }
                    if (fapReceiver.timer.isSynchronized())
                    {
                        currentAudio = audioReceiver.getCurrentAudioElement(characterTimer.getTimeMillis() / 40);
                    }
                }
            }
            else
            {
                if (fapReceiver.isConnected())
                {
                    fapReceiver.stopConnector();
                    thriftConsumerOpened = false;
                }
                if (bapReceiver.isConnected())
                {
                    bapReceiver.stopConnector();
                    thriftConsumerOpened = false;
                }
                if (audioReceiver.isConnected())
                {
                    audioReceiver.stopConnector();
                    thriftConsumerOpened = false;
                }
            }

            if (currentFAPFrame != null)
            {
                if (lastFAPFrame.isEqualTo(currentFAPFrame))
                {
                    cptFrames++;
                    if (cptFrames > 2)
                    {
                        agentPlaying = false;
                        cptFrames = 0;
                    }
                }
                else
                {
                    agentPlaying = true;
                    cptFrames = 0;
                    lastFAPFrame = new AnimationParametersFrame(currentFAPFrame);
                }



                if (directBlendShapeFromGreta)
                    applyBlendShapeFrame(currentFAPFrame);
                else if (DirectFapMapping)
                    applyFapFrameDirect(currentFAPFrame);
                else if (phonemeMode)
                    applyPhonemeFrame(currentFAPFrame);
                else
                    applyFapFrame(currentFAPFrame);
                
            }

            if (audioFilePlayer.isNewAudio() || audioReceiver.isNewAudio())
            {
                if (currentAudio.getSampleRate() > 0 && currentAudio.rawData.Length > 0)
                {
                    int len = currentAudio.rawData.Length / 2;
                    Destroy(_currentAudioSource.clip);

                    _currentAudioSource.clip = AudioClip.Create("text", len, 1, currentAudio.getSampleRate(), false);
                    float[] buffer = new float[len];
                    for (int iPCM = 44; iPCM < len; iPCM++)
                    {
                        float f;
                        short i = (short)((currentAudio.rawData[iPCM * 2 + 1] << 8) | currentAudio.rawData[iPCM * 2]);
                        f = ((float)i) / (float)32768;
                        if (f > 1) f = 1;
                        if (f < -1) f = -1;
                        buffer[iPCM] = f;
                    }
                    _currentAudioSource.clip.SetData(buffer, 0);

                    audioReceiver.setNewAudio(false);
                    audioFilePlayer.setNewAudio(false);
                }
                else
                {
                    if ((_currentAudioSource != null) && (_currentAudioSource.clip != null))
                    {
                        float offSet = ((float)characterTimer.getTimeMillis() - ((float)currentAudio.getFrameNumber() * 40)) / 1000;
                        int samplesOffset = (int)(_currentAudioSource.clip.frequency * offSet * _currentAudioSource.clip.channels);
                        _currentAudioSource.timeSamples = samplesOffset;
                    }
                    audioReceiver.setNewAudio(false);
                    audioFilePlayer.setNewAudio(false);
                }
            }
        }
        else
        {
            if (_currentAudioSource != null)
            {
                _currentAudioSource.Stop();
            }
        }
        if (animationIDold != animationID)
        {
            PlayAgentAnimation(animationID);
            animationIDold = animationID;
        }
    }

    public void OnApplicationQuit()
    {
        if (SceneManager.isThrift)
        {
            if (fapReceiver.isConnected())
            {
                fapReceiver.stopConnector();
            }
            if (bapReceiver.isConnected())
            {
                bapReceiver.stopConnector();
            }
            if (audioReceiver.isConnected())
            {
                audioReceiver.stopConnector();
            }
            if (commandSender.isConnected())
            {
                commandSender.stopConnector();
            }
        }
    }

    public void PlayAgentAnimation(string animationID, InterpersonalAttitude attitude = null)
    {
        animateAgent = true;
        if (distantConnection)
        {
            if (commandSender.isConnected())
            {
                commandSender.playAnimation(animationID, attitude);
            }
            else
            {
                Debug.LogWarning("AnimationReceiver on host: " + commandSender.getHost() + " and port: " + commandSender.getPort() + " not connected");
            }
        }
    }

    public void PlayAgentAnimation(string animationID)
    {
        animateAgent = true;
        if (distantConnection)
        {
            if (commandSender.isConnected())
                commandSender.playAnimation(animationID);
            else
                Debug.LogWarning("AnimationReceiver on host: " + commandSender.getHost() + " and port: " + commandSender.getPort() + " not connected");
        }
    }

    public void applyFapFrame(AnimationParametersFrame fapFrame)
    {
        foreach (var mapping in Model.Mappings)
        {
            float value = mapping.GetBlendShapeValue(fapFrame);
            foreach (var modelBS in mapping.modelBlendShape)
            {
                foreach (var (mesh, cache) in Model.MeshCaches)
                {
                    if (cache.TryGetValue(modelBS.blendShapeName, out int idx) && idx >= 0)
                        mesh.SetBlendShapeWeight(idx, value* modelBS.weight);
                    /*
                    if (cache.TryGetValue(modelBS.blendShapeName, out int idx) && idx >= 0)
                    {
                        float current = mesh.GetBlendShapeWeight(idx);
                        mesh.SetBlendShapeWeight(idx, Mathf.SmoothStep(current, value * modelBS.weight, 0.3f));
                    }
                    */
                }

            }
        }

    }

    public void applyBlendShapeFrame(AnimationParametersFrame fapFrame)
    {
        foreach (var mapping in Model.Mappings)
        {
            int arkitIdx = (int)mapping.ARKITBlendShape;
            if (arkitIdx >= fapFrame.size()) continue;

            float value = (float)fapFrame.getValue(arkitIdx) / 100;
            //if (arkitIdx == 24 || arkitIdx == 28)
            //    Debug.Log("Frame : " + fapFrame.getFrameNumber() +" / "+ arkitIdx + " : " + value * 100);

            foreach (var modelBS in mapping.modelBlendShape)
            {
                foreach (var (mesh, cache) in Model.MeshCaches)
                {
                    if (cache.TryGetValue(modelBS.blendShapeName, out int idx) && idx >= 0)
                        //if(arkitIdx != 24)
                        //    mesh.SetBlendShapeWeight(idx, value);
                        //else if( mesh.name == "CC_Base_Teeth")
                            mesh.SetBlendShapeWeight(idx, value);
                }
            }
        }
    }





    public void applyFapFrameDirect(AnimationParametersFrame fapFrame)
    {
        foreach (var mapping in DirectModel.Mappings)
        {
            float value = mapping.GetBlendShapeValue(fapFrame);
            foreach (var (mesh, cache) in DirectModel.MeshCaches)
            {
                if (cache.TryGetValue(mapping.blendShapeName, out int idx) && idx >= 0)
                    mesh.SetBlendShapeWeight(idx, value);
            }
        }
    }

    public void applyPhonemeFrame(AnimationParametersFrame fapFrame)
    {
        int phonemeIndex = 0;

        foreach (var kvp in blendshapeMapping)
        {
            string phoneme = kvp.Key;
            (string name, float weight)[] blendshapes = kvp.Value;

            float weight = fapFrame.getValue(phonemeIndex);

            foreach (var (bsName, bsWeight) in blendshapes)
            {
                foreach (var (mesh, cache) in visemeMeshCaches)
                {
                    if (cache.TryGetValue(bsName, out int idx) && idx >= 0)
                        mesh.SetBlendShapeWeight(idx, weight * bsWeight);
                }
            }

            phonemeIndex++;
        }
    }

    private Dictionary<string, int> MeshDataToDict(ModelBinding model)
    {
        var cache = new Dictionary<string, int>();
        var mesh = model.Mesh.sharedMesh; 

        foreach (var mapping in model.Mappings)
            foreach (var mbs in mapping.modelBlendShape)
            {
                if (cache.ContainsKey(mbs.blendShapeName)) continue;
                int idx = mesh.GetBlendShapeIndex(mbs.blendShapeName);
                cache[mbs.blendShapeName] = idx;
            }

        return cache;
    }
}
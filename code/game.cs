﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class game : MonoBehaviour
{
    public const float MIN_RENDER_RANGE = 0f;
    public const string PLAYER_PREFAB = "misc/player";
    public const float SLOW_UPDATE_TIME = 0.1f;

    public UnityEngine.UI.Text debug_text;
    public GameObject debug_panel;

    /// <summary> Information on how to start a game. </summary>
    public struct startup_info
    {
        public enum MODE
        {
            LOAD_AND_HOST,
            CREATE_AND_HOST,
            JOIN,
        }

        public MODE mode;
        public string username;
        public string world_name;
        public int world_seed;
        public string hostname;
        public int port;
    }
    public static startup_info startup;

    /// <summary> Load/host a game from disk. </summary>
    public static void load_and_host_world(string world_name, string username)
    {
        startup = new startup_info
        {
            username = username,
            mode = startup_info.MODE.LOAD_AND_HOST,
            world_name = world_name,
        };

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    /// <summary> Create/host a new world. </summary>
    public static void create_and_host_world(string world_name, int seed, string username)
    {
        startup = new startup_info
        {
            username = username,
            mode = startup_info.MODE.CREATE_AND_HOST,
            world_name = world_name,
            world_seed = seed,
        };

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    /// <summary> Join a world hosted on a server. </summary>
    public static bool join_world(string ip_port, string username)
    {
        string ip = ip_port.Split(':')[0];
        int port;
        if (!int.TryParse(ip_port.Split(':')[1], out port))
            return false;

        startup = new startup_info
        {
            username = username,
            mode = startup_info.MODE.JOIN,
            hostname = ip,
            port = port
        };

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
        return true;
    }

    /// <summary> The target render range, which the actual render range will lerp to. </summary>
    public static float render_range_target
    {
        get { return _render_range_target; }
        set
        {
            if (value < MIN_RENDER_RANGE) value = MIN_RENDER_RANGE;
            _render_range_target = value;
        }
    }
    private static float _render_range_target = chunk.SIZE;

    /// <summary> How far the player can see. </summary>
    public static float render_range
    {
        get { return _render_range; }
        private set
        {
            if (_render_range == value) return;
            if (value < MIN_RENDER_RANGE) value = MIN_RENDER_RANGE;
            _render_range = value;
            player.current.update_render_range();
        }
    }
    private static float _render_range = chunk.SIZE;

    void Start()
    {
        // Various startup modes
        switch (startup.mode)
        {
            case startup_info.MODE.LOAD_AND_HOST:
            case startup_info.MODE.CREATE_AND_HOST:

                // Start + join the server
                server.start(server.DEFAULT_PORT, startup.world_name, PLAYER_PREFAB);

                client.connect(network_utils.local_ip_address().ToString(),
                    server.DEFAULT_PORT, startup.username, "password");

                // Create the world (if required)
                if (startup.mode == startup_info.MODE.CREATE_AND_HOST)
                {
                    var w = (world)client.create(Vector3.zero, "misc/world");
                    w.networked_seed.value = startup.world_seed;
                    w.networked_name.value = startup.world_name;
                }

                break;

            case startup_info.MODE.JOIN:

                // Join the server
                client.connect(startup.hostname, startup.port,
                    startup.username, "password");

                break;

            default:
                throw new System.Exception("Unkown startup mode!");
        }

        // Start with invisible, locked cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Create the sky!
        create_sky();

        //if (Application.isEditor)
        QualitySettings.vSyncCount = 0;

        // Ensure we're using SRP batching
        UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching = true;

        // Debug panel starts closed
        debug_panel.SetActive(false);

        // Initialize options
        options_menu.initialize_options();

        // Set the slow_update method going
        InvokeRepeating("slow_update", 0, SLOW_UPDATE_TIME);
    }

    void create_sky()
    {
        // Create the sun
        var sun = FindObjectOfType<Light>();
        sun.transform.position = Vector3.zero;
        sun.transform.LookAt(new Vector3(1, -2, 1));
    }

    void Update()
    {
        // Toggle options on escape key
        if (Input.GetKeyDown(KeyCode.Escape))
            options_menu.open = !options_menu.open;

        // Toggle fullscreen modes on F11
        if (Input.GetKeyDown(KeyCode.F11))
        {
            switch (Screen.fullScreenMode)
            {
                case FullScreenMode.Windowed:
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    break;
                case FullScreenMode.FullScreenWindow:
                    Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                    break;
                case FullScreenMode.ExclusiveFullScreen:
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    break;
                default:
                    throw new System.Exception("Unkown fullscreen mode!");
            }
        }

        // Toggle the debug panel on F3
        if (Input.GetKeyDown(KeyCode.F3))
            debug_panel.SetActive(!debug_panel.activeInHierarchy);

        // Increase/Decrease render range on equals/minus keys
        if (Input.GetKeyDown(KeyCode.Equals)) render_range_target += 10f;
        if (Input.GetKeyDown(KeyCode.Minus)) render_range_target -= 10f;
        render_range = Mathf.Lerp(render_range, render_range_target, 3 * Time.deltaTime);

        // Run networking updates
        server.update();
        client.update();
    }

    /// <summary> Called every <see cref="SLOW_UPDATE_TIME"/> seconds. </summary>
    void slow_update()
    {
        if (!debug_panel.activeInHierarchy)
            return;

        string debug_text = "" +
            "\nWORLD\n" +
            world.info() + "\n" +
            "\nGRAPHICS\n" +
            graphics_info() + "\n" +
            "\nSERVER\n" +
            server.info() + "\n" +
            "\nCLIENT\n" +
            client.info() + "\n" +
            "\nPLAYER\n" +
            player.info() + "\n";

        debug_text = debug_text.Trim();

        // Allign all of the :'s precceded by a space
        int max_found = 0;
        foreach (var line in debug_text.Split('\n'))
        {
            int found = line.IndexOf(':');
            if (found > max_found)
            {
                if (line[found - 1] != ' ') continue;
                max_found = found;
            }
        }

        string padded = "";
        foreach (var line in debug_text.Split('\n'))
        {
            int found = line.IndexOf(':');
            string padded_line = line;
            if (found > 0)
            {
                padded_line = line.Substring(0, found);
                for (int i = 0; i < max_found - found; ++i)
                    padded_line += " ";
                padded_line += line.Substring(found);
            }
            padded += padded_line + "\n";
        }

        this.debug_text.text = padded;
    }

    void OnApplicationQuit()
    {
        // Disconnect from the network
        client.disconnect();
        server.stop();
    }

    public string graphics_info()
    {
        return "    FPS             : " + System.Math.Round(1 / Time.deltaTime, 0) + "\n" +
               "    Fullscreen mode : " + Screen.fullScreenMode;
    }
}

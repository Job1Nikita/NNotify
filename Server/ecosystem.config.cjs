module.exports = {
  apps: [
    {
      name: "nnotify-sync-server",
      script: "dist/index.js",
      cwd: "/opt/nnotify-sync-server",
      instances: 1,
      exec_mode: "fork",
      max_memory_restart: "300M",
      autorestart: true,
      watch: false,
      env: {
        NODE_ENV: "production",
        HOST: "0.0.0.0",
        PORT: "5334"
      }
    }
  ]
};
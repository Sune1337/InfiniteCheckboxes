const target = 'https://localhost:7066';

const PROXY_CONFIG = [
  {
    context: [
      "/api"
    ],
    target: target,
    secure: false,
    headers: {
      Connection: 'Keep-Alive'
    }
  }, {
    context: [
      "/hubs"
    ],
    target: target,
    secure: false,
    ws: true
  }
]

module.exports = PROXY_CONFIG;

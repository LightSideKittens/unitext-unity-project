module.exports = {
  use: { ignoreHTTPSErrors: true },
  timeout: parseInt(process.env.APP_TIMEOUT || '600') * 1000,
};

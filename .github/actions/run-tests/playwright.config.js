module.exports = {
  use: { ignoreHTTPSErrors: true },
  timeout: parseInt(process.env.TEST_TIMEOUT || '300') * 1000,
};

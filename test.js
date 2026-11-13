const fs = require('fs');

async function run() {
  try {
    const loginRes = await fetch("http://localhost:5000/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: "seller@retrack.vn", password: "Seller@123" })
    });
    const loginData = await loginRes.json();
    const token = loginData.data.token;

    const formData = new FormData();
    formData.append("file", new Blob(["hello world"], { type: "image/png" }), "test.png");

    const uploadRes = await fetch("http://localhost:5000/api/seller/pickups/upload", {
      method: "POST",
      headers: { Authorization: "Bearer " + token },
      body: formData
    });
    const result = await uploadRes.text();
    console.log("Status:", uploadRes.status);
    console.log("Result:", result);
  } catch (e) {
    console.error(e);
  }
}
run();

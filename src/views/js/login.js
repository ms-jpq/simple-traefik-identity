document.addEventListener("DOMContentLoaded", () => {
  const form = document.querySelector(`form`)
  const next = form.querySelector(`output[name="goto"]`).value

  const query = async (username, password) => {
    try {
      const params = {
        method: "POST",
        headers: {
          ["STI-Authorization"]: `Basic ${btoa(`${username}:${password}`)}`,
        }
      }
      return await (await fetch("/", params)).json()
    } catch (e) {
      alert(e.message)
    }
  }

  form.onsubmit = async (e) => {
    e.preventDefault()
    const { currentTarget: ct } = e
    const username = ct.querySelector(`input[name="username"]`).value
    const password = ct.querySelector(`input[name="password"]`).value
    const { ok, timeout } = await query(username, password)
    if (!ok) {
      const msg = timeout ? `⏳⌛️⏳` : `🙅‍♀️🔐`
      alert(msg)
    } else {
      location.href = next
    }
  }

  form.querySelector(`input[name="username"]`).focus()
})

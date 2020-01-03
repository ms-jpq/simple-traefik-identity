const main = () => {

  const query = async (username, password) => {
    try {
      const params = {
        method: "POST",
        headers: {
          ["STI-Authorization"]: `Basic ${btoa(`${username}:${password}`)}`,
        }
      }
      return (await fetch("/", params)).json()
    } catch (e) {
      alert(e.message)
    }
  }

  const submit = async (e) => {
    e.preventDefault()
    const { currentTarget: ct } = e
    const username = ct.querySelector(`input[name="username"]`).value
    const password = ct.querySelector(`input[name="password"]`).value
    const { ok, timeout } = await query(username, password)
    if (!ok) {
      const msg = timeout ? `⏳⌛️⏳` : `🙅‍♀️🔐`
      alert(msg)
    } else {
      location.reload()
    }
  }

  const form = document.querySelector(`form`)
  form.onsubmit = submit
  form.querySelector(`input[name="username"]`).focus()

  console.log("🙆‍♀️ -- Form Ready -- 🙆‍♀️")
}

main()
